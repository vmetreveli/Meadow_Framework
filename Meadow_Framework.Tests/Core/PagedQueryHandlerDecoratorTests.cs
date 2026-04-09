using FluentAssertions;
using Meadow.Abstractions.Queries;
using Meadow.Core.Queries;
using Xunit;

namespace Meadow_Framework.Tests.Core;

public class PagedQueryHandlerDecoratorTests
{
    [Theory]
    [InlineData(0, 0, 1, 10)]
    [InlineData(-1, -5, 1, 10)]
    [InlineData(3, 500, 3, 100)]
    [InlineData(2, 25, 2, 25)]
    public async Task Handle_ShouldNormalizePagingValues(int inputPage, int inputResults, int expectedPage, int expectedResults)
    {
        var query = new TestPagedQuery { Page = inputPage, Results = inputResults };
        var innerHandler = new CapturingPagedQueryHandler();
        var decorator = new PagedQueryHandlerDecorator<TestPagedQuery, string>(innerHandler);

        var result = await decorator.Handle(query);

        result.Should().Be("done");
        query.Page.Should().Be(expectedPage);
        query.Results.Should().Be(expectedResults);
        innerHandler.ReceivedQuery.Should().BeSameAs(query);
    }

    public sealed class TestPagedQuery : IQuery<string>, IPagedQuery
    {
        public int Page { get; set; }
        public int Results { get; set; }
    }

    public sealed class CapturingPagedQueryHandler : IQueryHandler<TestPagedQuery, string>
    {
        public TestPagedQuery? ReceivedQuery { get; private set; }

        public Task<string> Handle(TestPagedQuery query, CancellationToken cancellationToken = default)
        {
            ReceivedQuery = query;
            return Task.FromResult("done");
        }
    }
}
