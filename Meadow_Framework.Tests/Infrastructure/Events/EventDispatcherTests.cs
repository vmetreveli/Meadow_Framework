using FluentAssertions;
using MassTransit;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Kernel;
using Meadow_Framework.Core.Infrastructure.Events;
using Meadow_Framework.Core.Infrastructure.Exceptions;
using Meadow_Framework.Tests.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Meadow_Framework.Tests.Infrastructure.Events;

public class EventDispatcherTests
{
    private static EventDispatcher CreateDispatcher(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        configure?.Invoke(services);

        return new EventDispatcher(services.BuildServiceProvider());
    }

    private sealed class RecordingHandler : IDomainEventHandler<TestDomainEvent>
    {
        public List<TestDomainEvent> Handled { get; } = [];

        public Task HandleAsync(TestDomainEvent @event, CancellationToken cancellationToken = default)
        {
            Handled.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class SecondRecordingHandler : IDomainEventHandler<TestDomainEvent>
    {
        public List<TestDomainEvent> Handled { get; } = [];

        public Task HandleAsync(TestDomainEvent @event, CancellationToken cancellationToken = default)
        {
            Handled.Add(@event);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task PublishDomainEventAsync_ShouldInvokeRegisteredHandler()
    {
        var handler = new RecordingHandler();
        var dispatcher = CreateDispatcher(s => s.AddSingleton<IDomainEventHandler<TestDomainEvent>>(handler));
        var domainEvent = new TestDomainEvent("created");

        await dispatcher.PublishDomainEventAsync(domainEvent);

        handler.Handled.Should().ContainSingle().Which.Should().BeSameAs(domainEvent);
    }

    [Fact]
    public async Task PublishDomainEventAsync_ShouldResolveHandlersByRuntimeType_WhenPublishedAsInterface()
    {
        // Regression: callers such as DomainEventEntitiesInterceptor publish events typed as IDomainEvent.
        // Resolving handlers from the compile-time generic argument would silently drop the event.
        var handler = new RecordingHandler();
        var dispatcher = CreateDispatcher(s => s.AddSingleton<IDomainEventHandler<TestDomainEvent>>(handler));
        IDomainEvent domainEvent = new TestDomainEvent("created");

        await dispatcher.PublishDomainEventAsync(domainEvent);

        handler.Handled.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishDomainEventAsync_ShouldInvokeEveryHandlerForTheEvent()
    {
        var first = new RecordingHandler();
        var second = new SecondRecordingHandler();
        var dispatcher = CreateDispatcher(s =>
        {
            s.AddSingleton<IDomainEventHandler<TestDomainEvent>>(first);
            s.AddSingleton<IDomainEventHandler<TestDomainEvent>>(second);
        });

        await dispatcher.PublishDomainEventAsync(new TestDomainEvent("created"));

        first.Handled.Should().ContainSingle();
        second.Handled.Should().ContainSingle();
    }

    [Fact]
    public async Task PublishDomainEventAsync_ShouldNotThrow_WhenNoHandlerIsRegistered()
    {
        var dispatcher = CreateDispatcher();

        var publish = async () => await dispatcher.PublishDomainEventAsync(new TestDomainEvent("created"));

        await publish.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishIntegrationEventAsync_ShouldPublishToTheBroker()
    {
        var publishEndpoint = new Mock<IPublishEndpoint>();
        var dispatcher = CreateDispatcher(s => s.AddSingleton(publishEndpoint.Object));
        var integrationEvent = new TestIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow);

        await dispatcher.PublishIntegrationEventAsync(integrationEvent);

        // Published against the runtime type so MassTransit routes on the concrete contract.
        publishEndpoint.Verify(
            x => x.Publish(integrationEvent, typeof(TestIntegrationEvent), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishIntegrationEventAsync_ShouldThrowClearError_WhenEventBusIsNotRegistered()
    {
        var dispatcher = CreateDispatcher();

        var publish = async () =>
            await dispatcher.PublishIntegrationEventAsync(new TestIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow));

        await publish.Should().ThrowAsync<FrameworkException>()
            .WithMessage("*event bus is not registered*");
    }
}
