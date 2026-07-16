using MassTransit;
using Meadow_Framework.Core.Abstractions;
using Meadow_Framework.Core.Abstractions.Outbox;
using Meadow_Framework.Core.Abstractions.Repository;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Meadow_Framework.Core.Infrastructure.Jobs;

/// <summary>
///     Quartz job that publishes the outbox messages of a single <typeparamref name="TDbContext" /> to the
///     message broker and records the outcome against each message.
/// </summary>
/// <typeparam name="TDbContext">The context whose outbox this job drains.</typeparam>
[DisallowConcurrentExecution] // Ensures the job does not run concurrently.
public class OutboxJob<TDbContext>(
    IServiceProvider serviceProvider,
    MeadowOptions options,
    ILogger<OutboxJob<TDbContext>> logger) : IJob
    where TDbContext : DbContext, IDbContext
{
    /// <summary>
    ///     Publishes a batch of ready-to-send outbox messages.
    /// </summary>
    /// <param name="context">The Quartz execution context.</param>
    public async Task Execute(IJobExecutionContext context)
    {
        var cancellationToken = context.CancellationToken;

        using var scope = serviceProvider.CreateScope();

        var repository = scope.ServiceProvider.GetRequiredService<IOutboxRepository<TDbContext>>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var messages = await repository.GetAllReadyToSend(options.OutboxBatchSize, cancellationToken);

        if (messages.Count == 0)
            return;

        foreach (var message in messages)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var @event = message.RecreateMessage();

                // Publish against the runtime type so MassTransit routes on the concrete event contract.
                await publisher.Publish(@event, @event.GetType(), cancellationToken);

                message.ChangeState(OutboxMessageState.Completed);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One poisoned message must not stop the rest of the batch.
                message.RecordFailure(exception.ToString(), options.OutboxMaxRetries);

                logger.LogError(
                    exception,
                    "Failed to publish outbox message {OutboxMessageId} (event {EventId}, attempt {RetryCount} of {MaxRetries}).",
                    message.Id, message.EventId, message.RetryCount, options.OutboxMaxRetries);
            }
        }

        // Persist every outcome in the batch together. A crash before this point leaves the messages
        // ReadyToSend, so they are retried — delivery is at-least-once.
        await repository.SaveChangesAsync(cancellationToken);

        var failed = messages.Count(m => m.State == OutboxMessageState.Failed);
        if (failed > 0)
            logger.LogWarning(
                "{FailedCount} outbox message(s) for {DbContext} exhausted their retries and were marked Failed.",
                failed, typeof(TDbContext).Name);
    }
}
