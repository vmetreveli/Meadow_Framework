using System.Collections.Concurrent;
using System.Reflection;
using MassTransit;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Kernel;
using Meadow_Framework.Core.Abstractions.Primitives;
using Meadow_Framework.Core.Infrastructure.Exceptions;

namespace Meadow_Framework.Core.Infrastructure.Events;

/// <summary>
///     Dispatches domain events to in-process handlers and integration events to the message broker.
/// </summary>
/// <remarks>
///     This type is registered as scoped and resolves handlers from its own scope, so handlers share the
///     ambient <see cref="DbContext" /> and transaction with the code that raised the event.
/// </remarks>
public class EventDispatcher(IServiceProvider serviceProvider) : IEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> Invokers = new();

    /// <summary>
    ///     Publishes a domain event by invoking every registered handler for the event's runtime type.
    /// </summary>
    /// <typeparam name="TEvent">The type of domain event being published.</typeparam>
    /// <param name="event">The event instance to be published.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public async Task PublishDomainEventAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Resolve handlers by the event's runtime type, not TEvent: callers such as
        // DomainEventEntitiesInterceptor publish events typed as IDomainEvent, and no
        // handler is registered against the interface itself.
        var invoker = Invokers.GetOrAdd(@event.GetType(), HandlerInvoker.For);

        var handlers = serviceProvider.GetServices(invoker.HandlerType);

        // Handlers share this scope's DbContext, so they must not run concurrently.
        foreach (var handler in handlers)
        {
            if (handler is null)
                continue;

            await invoker.InvokeAsync(handler, @event, cancellationToken);
        }
    }

    /// <summary>
    ///     Publishes an integration event to the configured message broker.
    /// </summary>
    /// <typeparam name="TEvent">The type of integration event being published.</typeparam>
    /// <param name="event">The integration event instance to be published.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <exception cref="FrameworkException">The event bus is not registered.</exception>
    public async Task PublishIntegrationEventAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IntegrationBaseEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        var publisher = serviceProvider.GetService<IPublishEndpoint>()
                        ?? throw new FrameworkException(
                            "Cannot publish an integration event because the event bus is not registered. " +
                            "Enable it with AddFramework(..., needEvents: true).");

        // Publish against the runtime type so MassTransit routes on the concrete event contract
        // rather than on TEvent, which may be a base type at the call site.
        await publisher.Publish(@event, @event.GetType(), cancellationToken);
    }

    /// <summary>
    ///     Caches the reflection lookups needed to call <see cref="IDomainEventHandler{TEvent}.HandleAsync" />
    ///     for one concrete event type.
    /// </summary>
    private sealed class HandlerInvoker
    {
        private readonly MethodInfo _handleMethod;

        private HandlerInvoker(Type handlerType, MethodInfo handleMethod)
        {
            HandlerType = handlerType;
            _handleMethod = handleMethod;
        }

        public Type HandlerType { get; }

        public static HandlerInvoker For(Type eventType)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

            return new HandlerInvoker(handlerType, handleMethod);
        }

        public Task InvokeAsync(object handler, IDomainEvent @event, CancellationToken cancellationToken)
        {
            return (Task)_handleMethod.Invoke(handler, [@event, cancellationToken])!;
        }
    }
}
