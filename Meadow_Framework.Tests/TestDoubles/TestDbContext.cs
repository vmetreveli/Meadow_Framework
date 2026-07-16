using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Primitives;
using Meadow_Framework.Core.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Meadow_Framework.Tests.TestDoubles;

/// <summary>
///     A domain event raised by <see cref="TestAggregate" />.
/// </summary>
public sealed class TestDomainEvent : IDomainEvent
{
    public TestDomainEvent(string name)
    {
        Name = name;
    }

    public string Name { get; init; }
}

/// <summary>
///     An integration event used to exercise the outbox round trip.
/// </summary>
public sealed class TestIntegrationEvent : IntegrationBaseEvent
{
    public TestIntegrationEvent()
    {
    }

    public TestIntegrationEvent(Guid id, DateTime creationDate, string payload = "") : base(id, creationDate)
    {
        Payload = payload;
    }

    public string Payload { get; init; } = string.Empty;
}

/// <summary>
///     A minimal aggregate root that can buffer domain events on demand.
/// </summary>
public sealed class TestAggregate : AggregateRoot<Guid>
{
    public TestAggregate(Guid id, string name = "") : base(id)
    {
        Name = name;
    }

    public string Name { get; set; }

    public void Raise(IDomainEvent domainEvent)
    {
        RaiseDomainEvent(domainEvent);
    }
}

/// <summary>
///     A <see cref="BaseDbContext" /> backed by the in-memory provider, for tests that need a real
///     change tracker and interceptor pipeline.
/// </summary>
public sealed class TestDbContext(DbContextOptions options) : BaseDbContext(options)
{
    public DbSet<TestAggregate> Aggregates { get; set; } = null!;

    public static TestDbContext Create(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptors)
            .Options;

        return new TestDbContext(options);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestAggregate>(builder =>
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name);
        });
    }
}
