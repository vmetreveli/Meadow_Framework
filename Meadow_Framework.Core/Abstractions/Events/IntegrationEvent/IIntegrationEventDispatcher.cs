using Meadow_Framework.Core.Abstractions.Primitives;

namespace Meadow_Framework.Core.Abstractions.Events.IntegrationEvent;

/// <summary>
///
/// </summary>
public interface IIntegrationEventDispatcher
{
    /// <summary>
    ///     Publishes an integration event asynchronously.
    /// </summary>
    /// <typeparam name="TEvent">The type of the integration event to publish.</typeparam>
    /// <param name="event">The integration event to publish.</param>
    /// <param name="cancellationToken">A cancellation token to observe while waiting for the task to complete.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
  public  Task PublishIntegrationEventAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IntegrationBaseEvent;
}