using Meadow_Framework.Core.Abstractions.Dispatchers;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Events.DomainEvent;
using Meadow_Framework.Core.Abstractions.Primitives.Types;

namespace Meadow_Framework.Core.Infrastructure.Interceptors;

/// <summary>
///    Interceptor that converts domain events from aggregate roots into outbox messages before saving changes to the database.
/// </summary>
public sealed class DomainEventEntitiesInterceptor(IEventDispatcher eventDispatcher) : SaveChangesInterceptor
{
    /// <summary>
    ///
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            eventData.Context.ChangeTracker
                .Entries<IAggregateRoot>()
                .Select(x => x.Entity)
                .SelectMany(aggregateRoot =>
                {
                    IReadOnlyCollection<IDomainEvent> domainEvents = aggregateRoot.GetDomainEvents();

                    //aggregateRoot.ClearDomainEvents();

                    return domainEvents;
                }).ToList().ForEach((domainEvent) =>
                {
                    eventDispatcher.PublishDomainEventAsync(domainEvent, cancellationToken);
                });
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}