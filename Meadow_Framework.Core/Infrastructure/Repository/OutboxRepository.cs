using Meadow_Framework.Core.Abstractions;
using Meadow_Framework.Core.Abstractions.Outbox;
using Meadow_Framework.Core.Abstractions.Repository;
using Microsoft.EntityFrameworkCore;
using Outbox_OutboxMessage = Meadow_Framework.Core.Abstractions.Outbox.OutboxMessage;

namespace Meadow_Framework.Core.Infrastructure.Repository;

/// <summary>
///     Repository for Outbox pattern operations.
///     Scoped per DbContext (modular monolith safe).
/// </summary>
public class OutboxRepository<TDbContext>(
    TDbContext context)
    : RepositoryBase<TDbContext, Outbox_OutboxMessage, Guid>(context),
        IOutboxRepository<TDbContext>
    where TDbContext : DbContext, IDbContext
{
    /// <summary>
    ///     Creates outbox message if not exists (idempotent insert).
    /// </summary>
    public async Task CreateOutboxMessage(Outbox_OutboxMessage outboxMessage,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outboxMessage);

        var exists = await context.Set<Outbox_OutboxMessage>()
            .AnyAsync(x => x.EventId == outboxMessage.EventId, cancellationToken);

        if (!exists) context.Set<Outbox_OutboxMessage>().Add(outboxMessage);
    }

    /// <summary>
    ///     Updates message state safely.
    /// </summary>
    public async Task UpdateOutboxMessageState(Guid eventId, OutboxMessageState state,
        CancellationToken cancellationToken = default)
    {
        var outbox = await context.Set<Outbox_OutboxMessage>()
            .FirstOrDefaultAsync(m => m.EventId == eventId, cancellationToken);

        outbox?.ChangeState(state);
    }

    /// <summary>
    ///     Returns the oldest messages ready to be published, up to <paramref name="maxCount" />.
    /// </summary>
    public Task<List<Outbox_OutboxMessage>> GetAllReadyToSend(int maxCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCount);

        // Oldest first, so a burst of new events cannot starve messages already waiting.
        return context.Set<Outbox_OutboxMessage>()
            .Where(m => m.State == OutboxMessageState.ReadyToSend)
            .OrderBy(m => m.EventDate)
            .Take(maxCount)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    ///     Persists changes.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken);
    }
}
