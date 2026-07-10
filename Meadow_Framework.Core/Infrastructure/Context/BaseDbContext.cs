using Meadow_Framework.Core.Abstractions;

namespace Meadow_Framework.Core.Infrastructure.Context;

/// <summary>
///
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
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }
}