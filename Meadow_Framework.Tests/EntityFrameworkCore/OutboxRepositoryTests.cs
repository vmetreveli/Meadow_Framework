using FluentAssertions;
using Meadow.Abstractions.Outbox;
using Meadow.Abstractions.Primitives;
using Meadow.EntityFrameworkCore.Context;
using Meadow.EntityFrameworkCore.Repository;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Meadow_Framework.Tests.EntityFrameworkCore;

public class OutboxRepositoryTests
{
    [Fact]
    public async Task GetAllReadyToSend_ShouldReturnOnlyReadyMessages()
    {
        await using var context = CreateContext();
        context.OutboxMessages.AddRange(
            CreateOutboxMessage(Guid.NewGuid()),
            CreateOutboxMessage(Guid.NewGuid()));
        await context.SaveChangesAsync();

        var sentMessage = await context.OutboxMessages.FirstAsync();
        sentMessage.ChangeState(OutboxMessageState.SendToQueue);
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);
        var ready = await repository.GetAllReadyToSend();

        ready.Should().HaveCount(1);
        ready.Single().State.Should().Be(OutboxMessageState.ReadyToSend);
    }

    [Fact]
    public async Task UpdateOutboxMessageState_ShouldUpdateMessageState()
    {
        await using var context = CreateContext();
        var eventId = Guid.NewGuid();
        context.OutboxMessages.Add(CreateOutboxMessage(eventId));
        await context.SaveChangesAsync();

        var repository = new OutboxRepository(context);
        await repository.UpdateOutboxMessageState(eventId, OutboxMessageState.Completed);
        await repository.SaveChange();

        var updated = await context.OutboxMessages.SingleAsync(x => x.EventId == eventId);
        updated.State.Should().Be(OutboxMessageState.Completed);
    }

    private static BaseDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new BaseDbContext(options);
    }

    private static OutboxMessage CreateOutboxMessage(Guid eventId)
    {
        return new OutboxMessage(new TestIntegrationEvent(), eventId, DateTime.UtcNow)
        {
            Content = "test"
        };
    }

    public sealed class TestIntegrationEvent : IntegrationBaseEvent
    {
    }
}
