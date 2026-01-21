using Meadow.Framework.Domain.Abstractions.Events;
using Meadow.Framework.Domain.Abstractions.Primitives.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Newtonsoft.Json;
using OutboxMessage = Meadow.Framework.IntegrationEvents.OutboxMessage;

namespace Meadow.Framework.Outbox.Infrastructure.Interceptors;

/// <summary>
/// </summary>
public sealed class InsertOutboxMessagesInterceptor : SaveChangesInterceptor
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
        List<OutboxMessage> outboxMessages = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(x => x.Entity)
            .SelectMany(aggregateRoot =>
            {
                IReadOnlyCollection<IDomainEvent> domainEvents = aggregateRoot.GetDomainEvents();

                aggregateRoot.ClearDomainEvents();

                return domainEvents;
            })
            .Select(domainEvent => new OutboxMessage(
                JsonConvert.SerializeObject(
                    domainEvent,
                    new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    }), Guid.NewGuid(), DateTime.UtcNow))
            .ToList();

        await context.Set<OutboxMessage>()
            .AddRangeAsync(outboxMessages);
        return Task.CompletedTask;
    }
}