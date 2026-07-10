using MassTransit;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Kernel;
using Meadow_Framework.Core.Abstractions.Outbox;
using Meadow_Framework.Core.Abstractions.Primitives;
using Meadow_Framework.Core.Abstractions.Repository;

namespace Meadow_Framework.Core.Infrastructure.Events;

/// <summary>
///     EventDispatcher is responsible for dispatching domain events and integration events.
///     It leverages a service provider to resolve event handlers and a publish endpoint for sending integration events.
/// </summary>
public class EventDispatcher(
    IServiceProvider serviceProvider
   // IPublishEndpoint publisher,
   // IOutboxRepository repository,
    //IUnitOfWork unitOfWork
    ) : IEventDispatcher
{
    /// <summary>
    ///     Publishes a domain event asynchronously by invoking all registered event handlers for the given event type.
    /// </summary>
    /// <typeparam name="TEvent">The type of domain event being published.</typeparam>
    /// <param name="event">The event instance to be published.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public async Task PublishDomainEventAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IDomainEvent
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        // Resolve handlers by the event's runtime type, not TEvent: callers such as
        // DomainEventEntitiesInterceptor publish events typed as IDomainEvent, and no
        // handler is registered against the interface itself.
        Type handlerType = typeof(IDomainEventHandler<>).MakeGenericType(@event.GetType());
        List<object?> handlers = scope.ServiceProvider.GetServices(handlerType).ToList();

        if (handlers.Count == 0)
            return;

        System.Reflection.MethodInfo handleMethod =
            handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;

        IEnumerable<Task> tasks = handlers
            .Where(h => h is not null)
            .Select(h => (Task)handleMethod.Invoke(h, [@event, cancellationToken])!);

        await Task.WhenAll(tasks);
    }

    /// <summary>
    ///     Publishes an integration event asynchronously by sending it to the configured message broker.
    ///     If the event cannot be published, it is stored in the outbox for retrying.
    /// </summary>
    /// <typeparam name="TEvent">The type of integration event being published.</typeparam>
    /// <param name="event">The integration event instance to be published.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    public async Task PublishIntegrationEventAsync<TEvent>(
        TEvent @event,
        CancellationToken cancellationToken = default)
        where TEvent : IntegrationBaseEvent
    {
        // try
        // {
        //     // Publish the integration event to the message broker using MassTransit.
        //     await publisher.Publish(@event, cancellationToken);
        //
        //     // If the publish succeeds, update the corresponding outbox message state to 'Completed'.
        //     await repository.UpdateOutboxMessageState(@event.Id, OutboxMessageState.Completed);
        // }
        // catch (Exception ex)
        // {
        //     // In case of failure, store the event in the outbox for retrying.
        //     var outboxMessage = new OutboxMessage(@event, @event.Id, @event.CreationDate);
        //     repository.CreateOutboxMessage(outboxMessage);
        // }
        // finally
        // {
        //     // Persist changes to the repository (e.g., saving the outbox message).
        //     await repository.SaveChange();
        // }
    }
}