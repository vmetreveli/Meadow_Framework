using Meadow_Framework.Core.Abstractions.Events.DomainEvent;
using Meadow_Framework.Core.Abstractions.Events.IntegrationEvent;

namespace Meadow_Framework.Core.Abstractions.Dispatchers;

/// <summary>
///     Defines a dispatcher for publishing events.
/// </summary>
public interface IEventDispatcher: IDomainEventDispatcher, IIntegrationEventDispatcher
{
}