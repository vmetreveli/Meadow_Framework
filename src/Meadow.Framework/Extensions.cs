using Meadow.Framework.Infrastructure;
using Meadow.Framework.Exceptions;
using Meadow.Framework.Mediator;
using Meadow.Framework.Outbox;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Meadow.Framework;

/// <summary>
///     Provides extension methods for configuring the complete Meadow Framework.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Adds all Meadow Framework components: Core, Mediator, Outbox, and Exceptions.
    ///     This is a convenience method that registers all framework services at once.
    ///
    ///     Note: This method registers the basic framework services. For full functionality,
    ///     you may need to register additional services like:
    ///     - DbContext with specific database provider
    ///     - Repository implementations for your entities
    ///     - Unit of Work for your specific DbContext
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="assemblies">The assemblies to scan for handlers (commands, queries, events).</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    public static IServiceCollection AddMeadowFramework(
        this IServiceCollection services,
        IConfiguration configuration,
        params System.Reflection.Assembly[] assemblies)
    {
        return services
            .AddMeadowInfrastructure(configuration)
            .AddMeadowMediator(assemblies)
            .AddMeadowOutbox(configuration)
            .AddMeadowExceptions();
    }
}