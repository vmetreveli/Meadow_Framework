using FluentAssertions;
using Meadow.Abstractions.Events;
using Meadow.Abstractions.Primitives;
using Xunit;

namespace Meadow_Framework.Tests.Abstractions;

public class PrimitivesTests
{
    [Fact]
    public void ValueObject_WithSameAtomicValues_ShouldBeEqual()
    {
        var first = new Money(10, "USD");
        var second = new Money(10, "USD");

        (first == second).Should().BeTrue();
        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void ValueObject_WithDifferentRuntimeTypeButSameAtomicValues_ShouldBeEqual()
    {
        ValueObject first = new Money(10, "USD");
        ValueObject second = new PriceTag(10, "USD");

        first.Equals(second).Should().BeTrue();
    }

    [Fact]
    public void EntityBase_WithSameIdAndType_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var first = new UserEntity(id);
        var second = new UserEntity(id);

        (first == second).Should().BeTrue();
        first.Equals(second).Should().BeTrue();
    }

    [Fact]
    public void EntityBase_WithSameIdButDifferentType_ShouldNotBeEqual()
    {
        var id = Guid.NewGuid();
        var user = new UserEntity(id);
        var admin = new AdminEntity(id);

        user.Equals(admin).Should().BeFalse();
    }

    [Fact]
    public void AggregateRoot_ShouldStoreAndClearDomainEvents()
    {
        var aggregate = new TestAggregate(Guid.NewGuid());
        aggregate.Add(new SampleDomainEvent());
        aggregate.Add(new SampleDomainEvent());

        aggregate.GetDomainEvents().Should().HaveCount(2);

        aggregate.ClearDomainEvents();

        aggregate.GetDomainEvents().Should().BeEmpty();
    }

    public sealed class Money(int amount, string currency) : ValueObject
    {
        public override IEnumerable<object> GetAtomicValues()
        {
            yield return amount;
            yield return currency;
        }
    }

    public sealed class PriceTag(int amount, string currency) : ValueObject
    {
        public override IEnumerable<object> GetAtomicValues()
        {
            yield return amount;
            yield return currency;
        }
    }

    public sealed class UserEntity(Guid id) : EntityBase<Guid>(id);

    public sealed class AdminEntity(Guid id) : EntityBase<Guid>(id);

    public sealed class SampleDomainEvent : IDomainEvent;

    public sealed class TestAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Add(IDomainEvent domainEvent)
        {
            RaiseDomainEvent(domainEvent);
        }
    }
}
