using MassTransit;
using Meadow.Abstractions.Primitives;

namespace Meadow.MassTransit;

/// <summary>
///     Defines a consumer for handling integration events.
/// </summary>
/// <typeparam name="T">The type of the integration event to be handled.</typeparam>
public interface IEventConsumer<in T> : IConsumer<T> where T : IntegrationBaseEvent
{
    // The IConsumer<T> interface from MassTransit provides a method to handle messages of type T.
    // The T in this case must be an IntegrationBaseEvent, which means this consumer will handle integration events.
}