using FluentAssertions;
using Meadow.Abstractions.Commands;
using Meadow.Abstractions.Queries;
using Meadow.Core.Commands;
using Meadow.Core.Queries;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Meadow_Framework.Tests.Core;

/// <summary>
///
/// </summary>
public class CommandAndQueryDispatcherTests
{
    /// <summary>
    ///
    /// </summary>
    [Fact]
    public async Task CommandDispatcher_ShouldInvokeCommandHandler()
    {
        var handler = new TestCommandHandler();
        var services = new ServiceCollection();
        services.AddSingleton<ICommandHandler<TestCommand>>(handler);
        var provider = services.BuildServiceProvider();
        var dispatcher = new CommandDispatcher(provider);
        var cancellationToken = new CancellationTokenSource().Token;

        await dispatcher.SendAsync(new TestCommand(), cancellationToken);

        handler.WasCalled.Should().BeTrue();
        handler.ReceivedCancellationToken.Should().Be(cancellationToken);
    }

    /// <summary>
    ///
    /// </summary>
    [Fact]
    public async Task CommandDispatcher_WithResult_ShouldReturnHandlerResult()
    {
        var services = new ServiceCollection();
        services.AddScoped<ICommandHandler<ResultCommand, string>, ResultCommandHandler>();
        var provider = services.BuildServiceProvider();
        var dispatcher = new CommandDispatcher(provider);

        var result = await dispatcher.SendAsync<string>(new ResultCommand("ok"));

        result.Should().Be("ok");
    }

    /// <summary>
    ///
    /// </summary>
    [Fact]
    public async Task QueryDispatcher_ShouldReturnHandlerResult()
    {
        var handler = new TestQueryHandler();
        var services = new ServiceCollection();
        services.AddSingleton<IQueryHandler<TestQuery, int>>(handler);
        var provider = services.BuildServiceProvider();
        var dispatcher = new QueryDispatcher(provider);
        var cancellationToken = new CancellationTokenSource().Token;

        var result = await dispatcher.QueryAsync(new TestQuery(11), cancellationToken);

        result.Should().Be(22);
        handler.ReceivedCancellationToken.Should().Be(cancellationToken);
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class TestCommand : ICommand;

    /// <summary>
    ///
    /// </summary>
    /// <param name="value"></param>
    public sealed class ResultCommand(string value) : ICommand<string>
    {
        /// <summary>
        ///
        /// </summary>
        public string Value { get; } = value;
    }

    public sealed class TestQuery(int value) : IQuery<int>
    {
        public int Value { get; } = value;
    }

    public sealed class TestCommandHandler : ICommandHandler<TestCommand>
    {
        /// <summary>
        ///
        /// </summary>
        public bool WasCalled { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="command"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task Handle(TestCommand command, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ReceivedCancellationToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    public sealed class ResultCommandHandler : ICommandHandler<ResultCommand, string>
    {
        /// <summary>
        ///
        /// </summary>
        /// <param name="command"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<string> Handle(ResultCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(command.Value);
        }
    }

    /// <summary>
    ///
    /// </summary>
    public sealed class TestQueryHandler : IQueryHandler<TestQuery, int>
    {
        /// <summary>
        ///
        /// </summary>
        public CancellationToken ReceivedCancellationToken { get; private set; }

        /// <summary>
        ///
        /// </summary>
        /// <param name="query"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<int> Handle(TestQuery query, CancellationToken cancellationToken = default)
        {
            ReceivedCancellationToken = cancellationToken;
            return Task.FromResult(query.Value * 2);
        }
    }
}
