using Meadow_Framework.Core.Abstractions.Primitives;
using Meadow_Framework.Core.Infrastructure.Events;

namespace Meadow_Framework.Core.Abstractions.Events;

/// <summary>
///     Defines a dispatcher for publishing events.
/// </summary>
public interface IEventDispatcher : IDomainEventDispatcher, IIntegrationEventDispatcher
{

}