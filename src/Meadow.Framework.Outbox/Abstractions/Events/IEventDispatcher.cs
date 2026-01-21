using Meadow.Framework.Domain.Abstractions.Primitives;

namespace Meadow.Framework.Outbox.Abstractions.Events;

/// <summary>
///     Defines a dispatcher for publishing events.
/// </summary>
public interface IEventDispatcher
{
    /// <summary>
    ///     Publishes an integration event asynchronously.
    /// </summary>
    /// <typeparam name="TEvent">The type of the integration event to publish.</typeparam>
    /// <param name="event">The integration event to publish.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task PublishIntegrationEventAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationBaseEvent;
}