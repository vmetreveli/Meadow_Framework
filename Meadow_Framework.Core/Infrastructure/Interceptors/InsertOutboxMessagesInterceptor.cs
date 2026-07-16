using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Primitives.Types;
using Outbox_OutboxMessage = Meadow_Framework.Core.Abstractions.Outbox.OutboxMessage;

namespace Meadow_Framework.Core.Infrastructure.Interceptors;

/// <summary>
///     Converts the domain events buffered on tracked aggregate roots into outbox rows, written in the same
///     transaction as the state change that raised them.
/// </summary>
/// <remarks>
///     The rows are published to the message broker later by <c>OutboxJob</c>.
///     This interceptor is mutually exclusive with <see cref="DomainEventEntitiesInterceptor" />: both drain
///     the aggregates' event buffers. Select one with
///     <see cref="Meadow_Framework.Core.Infrastructure.MeadowOptions.DomainEventDispatchMode" />.
/// </remarks>
public sealed class InsertOutboxMessagesInterceptor : SaveChangesInterceptor
{
    /// <summary>
    ///     Called before changes are saved to the database.
    /// </summary>
    /// <param name="eventData">The event data associated with the database context.</param>
    /// <param name="result">The current result of the interception.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            ConvertDomainEventsToOutboxMessages(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ConvertDomainEventsToOutboxMessages(DbContext context)
    {
        var aggregateRoots = context.ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .ToList();

        var utcNow = DateTime.UtcNow;

        var outboxMessages = aggregateRoots
            .SelectMany(aggregateRoot => aggregateRoot.GetDomainEvents())
            .Select(domainEvent => new Outbox_OutboxMessage(domainEvent, Guid.NewGuid(), utcNow))
            .ToList();

        foreach (var aggregateRoot in aggregateRoots)
            aggregateRoot.ClearDomainEvents();

        if (outboxMessages.Count == 0)
            return;

        // AddRange rather than AddRangeAsync: the async overload only matters for value generators that
        // hit the database, and this must complete before SaveChanges builds its command list.
        context.Set<Outbox_OutboxMessage>().AddRange(outboxMessages);
    }
}
