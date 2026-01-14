using Meadow_Framework.Core.Abstractions.Primitives.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;
using Outbox_OutboxMessage = Meadow.Framework.Outbox.Abstractions.Outbox.OutboxMessage;

namespace Meadow.Framework.Outbox.Infrastructure.Interceptors;

/// <summary>
///
/// </summary>
public sealed class InsertOutboxMessagesInterceptor: SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _ = ConvertDomainEventsToOutboxMessages(eventData.Context);
            _ = ConvertDomainEventsToOutboxMessages(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static async Task<Task> ConvertDomainEventsToOutboxMessages(DbContext context)
    {
        List<Outbox_OutboxMessage> outboxMessages = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(x => x.Entity)
            .SelectMany(aggregateRoot =>
            {
                var domainEvents = aggregateRoot.GetDomainEvents();

                aggregateRoot.ClearDomainEvents();

                return domainEvents;
            })
            .Select(domainEvent => new Outbox_OutboxMessage(
                JsonConvert.SerializeObject(
                    domainEvent,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    }), Guid.NewGuid(), DateTime.UtcNow))
            .ToList();

        await context.Set<Outbox_OutboxMessage>()
            .AddRangeAsync(outboxMessages);
        return Task.CompletedTask;
    }
}