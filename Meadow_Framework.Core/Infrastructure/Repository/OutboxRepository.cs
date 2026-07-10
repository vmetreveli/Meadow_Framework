using Meadow_Framework.Core.Abstractions.Outbox;
using Meadow_Framework.Core.Abstractions.Repository;
using Meadow_Framework.Core.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;
using Outbox_OutboxMessage = Meadow_Framework.Core.Abstractions.Outbox.OutboxMessage;

namespace Meadow_Framework.Core.Infrastructure.Repository;

/// <summary>
/// Repository for Outbox pattern operations.
/// Scoped per DbContext (modular monolith safe).
/// </summary>
public class OutboxRepository<TDbContext>(
    TDbContext context)
    : RepositoryBase<TDbContext, Outbox_OutboxMessage, Guid>(context),
        IOutboxRepository<TDbContext>
    where TDbContext : BaseDbContext
{
    /// <summary>
    /// Creates outbox message if not exists (idempotent insert).
    /// </summary>
    public async Task CreateOutboxMessage(Outbox_OutboxMessage outboxMessage, CancellationToken cancellationToken = default)
    {
        var exists = await context.OutboxMessages
            .AnyAsync(x => x.EventId == outboxMessage.EventId, cancellationToken: cancellationToken);

        if (!exists)
        {
            context.OutboxMessages.Add(outboxMessage);
        }
    }

    /// <summary>
    /// Updates message state safely.
    /// </summary>
    public async Task UpdateOutboxMessageState(Guid eventId, OutboxMessageState state, CancellationToken cancellationToken = default)
    {
        var outbox = await context.OutboxMessages
            .FirstOrDefaultAsync(m => m.EventId == eventId, cancellationToken: cancellationToken);

        if (outbox is null)
            return;

        outbox.ChangeState(state);
    }

    /// <summary>
    /// Returns messages ready to be published.
    /// </summary>
    public Task<List<Outbox_OutboxMessage>> GetAllReadyToSend(CancellationToken cancellationToken = default)
    {
        return context.OutboxMessages
            .Where(m => m.State == OutboxMessageState.ReadyToSend)
            .ToListAsync(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Persists changes.
    /// </summary>
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return context.SaveChangesAsync(cancellationToken: cancellationToken);
    }
}