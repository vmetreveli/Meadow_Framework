using Meadow_Framework.Core.Abstractions.Outbox;

namespace Meadow_Framework.Core.Abstractions.Repository;

/// <summary>
///     Contract for the outbox store of a single <see cref="DbContext" />, so that each module of a
///     modular monolith publishes from its own transaction.
/// </summary>
/// <typeparam name="TDbContext">The context that owns the outbox table.</typeparam>
public interface IOutboxRepository<TDbContext>
    where TDbContext : DbContext, IDbContext
{
    /// <summary>
    ///     Creates a new outbox message if it does not already exist.
    /// </summary>
    Task CreateOutboxMessage(
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Updates state of an existing outbox message.
    /// </summary>
    Task UpdateOutboxMessageState(
        Guid eventId,
        OutboxMessageState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Gets the oldest messages that are ready to be published, up to <paramref name="maxCount" />.
    /// </summary>
    /// <param name="maxCount">The maximum number of messages to return.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task<List<OutboxMessage>> GetAllReadyToSend(
        int maxCount,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Persists changes to database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
