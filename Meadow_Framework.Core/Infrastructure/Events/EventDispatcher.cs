using MassTransit;
using Meadow_Framework.Core.Abstractions.Dispatchers;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Events.DomainEvent;
using Meadow_Framework.Core.Abstractions.Primitives;
using Microsoft.Extensions.DependencyInjection;

namespace Meadow_Framework.Core.Infrastructure.Events;

/// <summary>
/// Dispatches domain and integration events.
/// </summary>
public class EventDispatcher(
    IServiceProvider serviceProvider,
    IPublishEndpoint publisher)
    : IEventDispatcher
{
    /// <summary>
    /// Dispatches the specified domain event.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEvent"></typeparam>
    public async Task PublishDomainEventAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : class, IDomainEvent
    {

    }

    /// <summary>
    /// Publishes the specified integration event.
    /// </summary>
    /// <param name="event"></param>
    /// <param name="cancellationToken"></param>
    /// <typeparam name="TEvent"></typeparam>
    public async Task PublishIntegrationEventAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IntegrationBaseEvent
    {
        try
        {
            await publisher.Publish(@event, cancellationToken);
        }
        catch
        {
            /*
             * IMPORTANT:
             * EventDispatcher should NOT know DbContext.
             * Outbox persistence MUST be handled BEFORE calling this method
             * (via OutboxJob or application layer).
             */
            throw;
        }
    }


    public async Task PublishDomainEventAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        await publisher.Publish(@event, cancellationToken);
    }

    public Task PublishDomainEventAsync(IDomainEvent[] events, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}