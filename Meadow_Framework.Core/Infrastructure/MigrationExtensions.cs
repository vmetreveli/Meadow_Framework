using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Meadow_Framework.Core.Abstractions;

namespace Meadow_Framework.Core.Infrastructure;

/// <summary>
/// Extension methods for applying database migrations at application startup.
/// </summary>
public static class MigrationExtensions
{
    /// <summary>
    /// Applies database migrations at application startup.
    /// </summary>
    /// <param name="app"></param>
    /// <param name="assemblies"></param>
    /// <returns></returns>
    public static WebApplication ApplyMigration(this WebApplication app, IEnumerable<Assembly> assemblies)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;

        var logger = services.GetRequiredService<ILogger<MigrationExtensionsLogger>>();

        var dbContextTypes = FindDbContexts(assemblies);

        foreach (var dbContextType in dbContextTypes)
        {
            try
            {
                logger.LogInformation("Starting migration for {DbContextName}", dbContextType.Name);

                if (services.GetService(dbContextType) is not DbContext context)
                {
                    logger.LogWarning("DbContext not registered: {DbContextName}", dbContextType.Name);
                    continue;
                }

                context.Database.Migrate();

                logger.LogInformation("Migration completed for {DbContextName}", dbContextType.Name);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Migration failed for {DbContextName}", dbContextType.FullName);
            }
        }

        return app;
    }

    private static IEnumerable<Type> FindDbContexts(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t =>
                !t.IsAbstract &&
                typeof(DbContext).IsAssignableFrom(t) &&
                typeof(IDbContext).IsAssignableFrom(t));
    }
}

public class MigrationExtensionsLogger { }