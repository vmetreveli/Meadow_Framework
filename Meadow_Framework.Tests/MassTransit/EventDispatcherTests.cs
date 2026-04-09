using FluentAssertions;
using MassTransit;
using Meadow.Abstractions.Outbox;
using Meadow.Abstractions.Primitives;
using Meadow.Abstractions.Repository;
using Meadow.MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Meadow_Framework.Tests.MassTransit;

public class EventDispatcherTests
{
    [Fact]
    public async Task PublishIntegrationEventAsync_WhenPublishSucceeds_ShouldMarkOutboxAsCompleted()
    {
        var publisher = new Mock<IPublishEndpoint>();
        publisher
            .Setup(x => x.Publish(It.IsAny<TestIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var repository = new Mock<IOutboxRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new EventDispatcher(provider, publisher.Object, repository.Object, unitOfWork.Object);
        var integrationEvent = new TestIntegrationEvent();

        await dispatcher.PublishIntegrationEventAsync(integrationEvent);

        repository.Verify(
            x => x.UpdateOutboxMessageState(integrationEvent.Id, OutboxMessageState.Completed),
            Times.Once);
        repository.Verify(x => x.CreateOutboxMessage(It.IsAny<OutboxMessage>()), Times.Never);
        repository.Verify(x => x.SaveChange(), Times.Once);
    }

    [Fact]
    public async Task PublishIntegrationEventAsync_WhenPublishFails_ShouldStoreOutboxMessage()
    {
        var publisher = new Mock<IPublishEndpoint>();
        publisher
            .Setup(x => x.Publish(It.IsAny<TestIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var repository = new Mock<IOutboxRepository>();
        var unitOfWork = new Mock<IUnitOfWork>();
        using var provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new EventDispatcher(provider, publisher.Object, repository.Object, unitOfWork.Object);
        var integrationEvent = new TestIntegrationEvent();

        await dispatcher.PublishIntegrationEventAsync(integrationEvent);

        repository.Verify(
            x => x.CreateOutboxMessage(It.Is<OutboxMessage>(m => m.EventId == integrationEvent.Id)),
            Times.Once);
        repository.Verify(
            x => x.UpdateOutboxMessageState(It.IsAny<Guid>(), It.IsAny<OutboxMessageState>()),
            Times.Never);
        repository.Verify(x => x.SaveChange(), Times.Once);
    }

    public sealed class TestIntegrationEvent : IntegrationBaseEvent
    {
    }
}
