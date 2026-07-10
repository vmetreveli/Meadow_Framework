using Meadow_Framework.Core.Abstractions.Outbox;
using Meadow_Framework.Core.Infrastructure.Context;

namespace Meadow_Framework.Core.Abstractions.Repository;

/// <summary>
/// Contract for Outbox repository (modular monolith safe).
/// </summary>
public interface IOutboxRepository<TDbContext>
    where TDbContext : BaseDbContext
{
    /// <summary>
    /// Creates a new outbox message if it does not already exist.
    /// </summary>
    Task CreateOutboxMessage(
        OutboxMessage outboxMessage,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates state of an existing outbox message.
    /// </summary>
    Task UpdateOutboxMessageState(
        Guid eventId,
        OutboxMessageState state,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages ready to be published.
    /// </summary>
    Task<List<OutboxMessage>> GetAllReadyToSend(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists changes to database.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}