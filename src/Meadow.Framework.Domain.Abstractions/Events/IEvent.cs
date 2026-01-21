namespace Meadow.Framework.Domain.Abstractions.Events;

/// <summary>
///     Marker interface for domain events.
/// </summary>
/// <remarks>
///     This interface is used as a marker to distinguish domain events
///     within the system. Domain events represent significant changes
///     in the domain that other parts of the system might be interested in.
/// </remarks>
public interface IEvent
{
    // This interface does not define any additional members. It serves as a
    // marker interface to identify domain events in the system.
}