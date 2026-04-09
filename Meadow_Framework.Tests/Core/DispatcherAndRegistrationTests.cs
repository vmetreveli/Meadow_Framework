using FluentAssertions;
using Meadow.Abstractions.Commands;
using Meadow.Abstractions.Dispatchers;
using Meadow.Abstractions.Queries;
using Meadow.Core;
using Meadow.Core.Dispatchers;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Meadow_Framework.Tests.Core;

public class DispatcherAndRegistrationTests
{
    [Fact]
    public async Task Dispatcher_ShouldDelegateToCommandAndQueryDispatchers()
    {
        var commandDispatcher = new Mock<ICommandDispatcher>();
        var queryDispatcher = new Mock<IQueryDispatcher>();
        queryDispatcher
            .Setup(x => x.QueryAsync(It.IsAny<RegistrationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("query-result");
        commandDispatcher
            .Setup(x => x.SendAsync(It.IsAny<RegistrationResultCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("command-result");

        var dispatcher = new Dispatcher(commandDispatcher.Object, queryDispatcher.Object);

        await dispatcher.SendAsync(new RegistrationCommand());
        var commandResult = await dispatcher.SendAsync<string>(new RegistrationResultCommand());
        var queryResult = await dispatcher.QueryAsync(new RegistrationQuery());

        commandResult.Should().Be("command-result");
        queryResult.Should().Be("query-result");
        commandDispatcher.Verify(x => x.SendAsync(It.IsAny<RegistrationCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        commandDispatcher.Verify(x => x.SendAsync(It.IsAny<RegistrationResultCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        queryDispatcher.Verify(x => x.QueryAsync(It.IsAny<RegistrationQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void AddMeadowCore_ShouldRegisterDispatchersAndHandlersFromAssembly()
    {
        var services = new ServiceCollection();

        services.AddMeadowCore(typeof(RegistrationCommandHandler).Assembly);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var scopedProvider = scope.ServiceProvider;

        scopedProvider.GetService<IDispatcher>().Should().NotBeNull();
        scopedProvider.GetService<ICommandDispatcher>().Should().NotBeNull();
        scopedProvider.GetService<IQueryDispatcher>().Should().NotBeNull();
        scopedProvider.GetService<ICommandHandler<RegistrationCommand>>().Should().NotBeNull();
        scopedProvider.GetService<ICommandHandler<RegistrationResultCommand, string>>().Should().NotBeNull();
        scopedProvider.GetService<IQueryHandler<RegistrationQuery, string>>().Should().NotBeNull();
    }

    public sealed class RegistrationCommand : ICommand;

    public sealed class RegistrationResultCommand : ICommand<string>;

    public sealed class RegistrationQuery : IQuery<string>;

    public sealed class RegistrationCommandHandler : ICommandHandler<RegistrationCommand>
    {
        public Task Handle(RegistrationCommand command, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    public sealed class RegistrationResultCommandHandler : ICommandHandler<RegistrationResultCommand, string>
    {
        public Task<string> Handle(RegistrationResultCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("registered");
        }
    }

    public sealed class RegistrationQueryHandler : IQueryHandler<RegistrationQuery, string>
    {
        public Task<string> Handle(RegistrationQuery query, CancellationToken cancellationToken = default)
        {
            return Task.FromResult("registered");
        }
    }
}
