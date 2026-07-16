using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Primitives.Types;

namespace Meadow_Framework.Core.Infrastructure.Interceptors;

/// <summary>
///     Dispatches the domain events buffered on tracked aggregate roots to their in-process handlers as part
///     of <see cref="DbContext.SaveChangesAsync(CancellationToken)" />.
/// </summary>
/// <remarks>
///     Handlers run before the save completes and share the ambient <see cref="DbContext" />, so any changes
///     they make are persisted by the same <c>SaveChanges</c> call and the same transaction. Handlers must
///     therefore not call <c>SaveChanges</c> themselves.
///     This interceptor is mutually exclusive with <see cref="InsertOutboxMessagesInterceptor" />: both drain
///     the aggregates' event buffers. Select one with
///     <see cref="Meadow_Framework.Core.Infrastructure.MeadowOptions.DomainEventDispatchMode" />.
/// </remarks>
public sealed class DomainEventEntitiesInterceptor(
    IEventDispatcher eventDispatcher
) : SaveChangesInterceptor
{
    /// <summary>
    ///     Called before changes are saved to the database.
    /// </summary>
    /// <param name="eventData">The event data associated with the database context.</param>
    /// <param name="result">The current result of the interception.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
            await DispatchDomainEventsAsync(eventData.Context, cancellationToken);

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext context, CancellationToken cancellationToken)
    {
        var aggregateRoots = context
            .ChangeTracker
            .Entries<IAggregateRoot>()
            .Select(entry => entry.Entity)
            .ToList();

        // Drain every buffer before dispatching: a handler may touch a tracked aggregate, and leaving the
        // events in place while dispatching would let them be picked up a second time.
        var domainEvents = aggregateRoots
            .SelectMany(aggregateRoot => aggregateRoot.GetDomainEvents())
            .ToList();

        foreach (var aggregateRoot in aggregateRoots)
            aggregateRoot.ClearDomainEvents();

        foreach (var domainEvent in domainEvents)
            await eventDispatcher.PublishDomainEventAsync(domainEvent, cancellationToken);
    }
}
