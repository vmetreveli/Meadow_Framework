namespace Meadow.Framework.Domain.Abstractions.Events;

/// <summary>
///     Marker interface for domain events.
/// </summary>
/// <remarks>
///     This interface extends the <see cref="IEvent" /> interface to specifically
///     identify domain events within the system. Domain events are events that
///     occur as a result of a domain operation and are used to communicate state
///     changes or trigger additional business logic within the domain.
/// </remarks>
public interface IDomainEvent : IEvent
{
}
