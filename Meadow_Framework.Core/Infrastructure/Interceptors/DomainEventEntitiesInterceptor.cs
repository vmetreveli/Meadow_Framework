using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Primitives.Types;

namespace Meadow_Framework.Core.Infrastructure.Interceptors;

/// <summary>
///    Interceptor that converts domain events from aggregate roots into outbox messages before saving changes to the database.
/// </summary>
public sealed class DomainEventEntitiesInterceptor(
    IEventDispatcher eventDispatcher
    ) : SaveChangesInterceptor
{
    /// <summary>
    ///   Called before changes are saved to the database.
    /// </summary>
    /// <param name="eventData"></param>
    /// <param name="result"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            IEnumerable<IAggregateRoot> aggregateRoots = eventData
                .Context
                .ChangeTracker
                .Entries<IAggregateRoot>()
                .Select(x => x.Entity);

            foreach (IAggregateRoot aggregateRoot in aggregateRoots)
            {
                IReadOnlyCollection<IDomainEvent> domainEvents = aggregateRoot.GetDomainEvents();
                foreach (IDomainEvent domainEvent in domainEvents)
                {
                    await eventDispatcher.PublishDomainEventAsync(domainEvent, cancellationToken);
                }

                aggregateRoot.ClearDomainEvents();
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}