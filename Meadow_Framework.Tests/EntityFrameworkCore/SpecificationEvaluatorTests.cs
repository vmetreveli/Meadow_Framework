using FluentAssertions;
using Meadow.Abstractions.Primitives;
using Meadow.Abstractions.Specifications;
using Meadow.EntityFrameworkCore.Specifications;
using Xunit;

namespace Meadow_Framework.Tests.EntityFrameworkCore;

public class SpecificationEvaluatorTests
{
    [Fact]
    public void GetQuery_ShouldApplyCriteriaAndAscendingOrder()
    {
        var data = new List<TaskEntity>
        {
            new(1, "B", 1),
            new(2, "C", 3),
            new(3, "A", 2)
        }.AsQueryable();

        var specification = new HighPriorityAscendingSpecification();

        var query = SpecificationEvaluator.GetQuery(data, specification);
        var result = query.ToList();

        result.Select(x => x.Title).Should().Equal("A", "C");
    }

    [Fact]
    public void GetQuery_ShouldApplyDescendingOrderWhenSpecified()
    {
        var data = new List<TaskEntity>
        {
            new(1, "B", 1),
            new(2, "C", 3),
            new(3, "A", 2)
        }.AsQueryable();

        var specification = new DescendingSpecification();

        var query = SpecificationEvaluator.GetQuery(data, specification);
        var result = query.ToList();

        result.Select(x => x.Title).Should().Equal("C", "B", "A");
    }

    public sealed class TaskEntity(int id, string title, int priority) : EntityBase<int>(id)
    {
        public string Title { get; private set; } = title;
        public int Priority { get; private set; } = priority;
    }

    public sealed class HighPriorityAscendingSpecification : Specification<TaskEntity, int>
    {
        public HighPriorityAscendingSpecification() : base(x => x.Priority >= 2)
        {
            AddOrderBy(x => x.Title);
        }
    }

    public sealed class DescendingSpecification : Specification<TaskEntity, int>
    {
        public DescendingSpecification() : base(null)
        {
            AddOrderByDescending(x => x.Title);
        }
    }
}
