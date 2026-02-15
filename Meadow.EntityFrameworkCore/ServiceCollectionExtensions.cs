using Meadow.Abstractions;
using Meadow.Abstractions.Repository;
using Meadow.Core.Seed;
using Meadow.EntityFrameworkCore.Context;
using Meadow.EntityFrameworkCore.Interceptors;
using Meadow.EntityFrameworkCore.Repository;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Meadow.EntityFrameworkCore;

/// <summary>
///     Extension methods for registering Meadow.EntityFrameworkCore services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Adds Meadow EF Core services: DbContext, UnitOfWork, OutboxRepository, and interceptors.
    /// </summary>
    public static IServiceCollection AddMeadowEfCore<TContext>(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionStringName = "DefaultConnection")
        where TContext : BaseDbContext
    {
        services.AddScoped<IUnitOfWork, UnitOfWork<TContext>>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddSingleton<InsertOutboxMessagesInterceptor>();
        services.AddSingleton<UpdateAuditableEntitiesInterceptor>();
        services.AddSingleton<UpdateDeletableEntitiesInterceptor>();

        services.AddDbContext<TContext>((sp, options) =>
        {
            options.UseNpgsql(configuration.GetConnectionString(connectionStringName))
                .UseSnakeCaseNamingConvention()
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .AddInterceptors(
                    sp.GetRequiredService<InsertOutboxMessagesInterceptor>(),
                    sp.GetRequiredService<UpdateAuditableEntitiesInterceptor>(),
                    sp.GetRequiredService<UpdateDeletableEntitiesInterceptor>());
        });

        return services;
    }

    /// <summary>
    ///     Applies pending database migrations and runs data seeders.
    /// </summary>
    public static IApplicationBuilder UseMigration<TContext>(this IApplicationBuilder app)
        where TContext : DbContext
    {
        MigrateDatabaseAsync<TContext>(app.ApplicationServices).GetAwaiter().GetResult();
        SeedDataAsync(app.ApplicationServices).GetAwaiter().GetResult();
        return app;
    }

    private static async Task MigrateDatabaseAsync<TContext>(IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        await context.Database.MigrateAsync();
    }

    private static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var seeders = scope.ServiceProvider.GetServices<IDataSeeder>();
        foreach (var seeder in seeders) await seeder.SeedAllAsync();
    }
}
