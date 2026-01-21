using Meadow.Framework.Infrastructure.Infrastructure.Interceptors;
using Meadow.Framework.Infrastructure.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Meadow.Framework.Infrastructure;

/// <summary>
///     Provides extension methods for configuring Meadow Framework Core services.
/// </summary>
public static class Extensions
{
    /// <summary>
    ///     Adds Meadow Framework Core services including interceptors and security services.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection" /> to add the services to.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The modified <see cref="IServiceCollection" />.</returns>
    public static IServiceCollection AddMeadowInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register interceptors for auditing and soft deletes
        services.AddScoped<UpdateAuditableEntitiesInterceptor>();
        services.AddScoped<UpdateDeletableEntitiesInterceptor>();

        // Register security services
        services.AddSingleton<MaskedStringJsonConverter>();

        return services;
    }

    /// <summary>
    ///     Configures Entity Framework Core DbContext with Meadow Framework interceptors.
    /// </summary>
    /// <typeparam name="TDbContext">The type of the DbContext to configure.</typeparam>
    /// <param name="optionsBuilder">The options builder for the DbContext.</param>
    /// <returns>The configured options builder.</returns>
    public static DbContextOptionsBuilder<TDbContext> UseMeadowInterceptors<TDbContext>(
        this DbContextOptionsBuilder<TDbContext> optionsBuilder)
        where TDbContext : DbContext
    {
        optionsBuilder.AddInterceptors(
            new UpdateAuditableEntitiesInterceptor(),
            new UpdateDeletableEntitiesInterceptor());

        return optionsBuilder;
    }

}