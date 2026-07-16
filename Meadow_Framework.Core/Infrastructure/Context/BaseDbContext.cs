using Meadow_Framework.Core.Abstractions;
using Meadow_Framework.Core.Infrastructure.Context.Configurations;

namespace Meadow_Framework.Core.Infrastructure.Context;

/// <summary>
///     Base class for framework-aware database contexts. Provides the outbox storage and applies
///     entity configurations found in the derived context's own assembly.
/// </summary>
public abstract class BaseDbContext(DbContextOptions options) : DbContext(options), IDbContext
{
    /// <summary>
    ///     Gets or sets the DbSet for OutboxMessage entities.
    /// </summary>
    public DbSet<OutboxMessage> OutboxMessages { get; set; }

    /// <summary>
    ///     Configures the model that was discovered from the entity types.
    /// </summary>
    /// <param name="modelBuilder"></param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // The framework's own configurations, then the derived context's assembly, which is where a
        // consuming application declares its entity configurations.
        modelBuilder.ApplyConfiguration(new OutboxMessageConfig());
        modelBuilder.ApplyConfigurationsFromAssembly(GetType().Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
