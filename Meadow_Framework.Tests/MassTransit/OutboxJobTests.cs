using FluentAssertions;
using Meadow.Abstractions.Outbox;
using Meadow.Abstractions.Primitives;
using Meadow.Abstractions.Repository;
using Meadow.MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Quartz;
using Xunit;

namespace Meadow_Framework.Tests.MassTransit;

public class OutboxJobTests
{
    [Fact]
    public async Task Execute_ShouldPublishAllReadyMessages()
    {
        var readyMessages = new List<OutboxMessage>
        {
            new(new TestIntegrationEvent(), Guid.NewGuid(), DateTime.UtcNow),
            new(new TestIntegrationEvent(), Guid.NewGuid(), DateTime.UtcNow)
        };

        var repository = new Mock<IOutboxRepository>();
        repository.Setup(x => x.GetAllReadyToSend()).ReturnsAsync(readyMessages);

        var eventDispatcher = new Mock<Meadow.Abstractions.Events.IEventDispatcher>();
        var services = new ServiceCollection();
        services.AddScoped(_ => repository.Object);
        using var provider = services.BuildServiceProvider();

        var job = new OutboxJob(provider, eventDispatcher.Object);

        await job.Execute(Mock.Of<IJobExecutionContext>());

        eventDispatcher.Verify(
            x => x.PublishIntegrationEventAsync(It.IsAny<TestIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Execute_WhenNoMessages_ShouldNotPublish()
    {
        var repository = new Mock<IOutboxRepository>();
        repository.Setup(x => x.GetAllReadyToSend()).ReturnsAsync([]);

        var eventDispatcher = new Mock<Meadow.Abstractions.Events.IEventDispatcher>();
        var services = new ServiceCollection();
        services.AddScoped(_ => repository.Object);
        using var provider = services.BuildServiceProvider();

        var job = new OutboxJob(provider, eventDispatcher.Object);

        await job.Execute(Mock.Of<IJobExecutionContext>());

        eventDispatcher.Invocations.Should().BeEmpty();
    }

    public sealed class TestIntegrationEvent : IntegrationBaseEvent
    {
    }
}
