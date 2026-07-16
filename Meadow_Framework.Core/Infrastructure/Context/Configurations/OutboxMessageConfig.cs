using Meadow_Framework.Core.Abstractions.Outbox;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Outbox_OutboxMessage = Meadow_Framework.Core.Abstractions.Outbox.OutboxMessage;

namespace Meadow_Framework.Core.Infrastructure.Context.Configurations;

/// <summary>
///     Configuration class for the <see cref="OutboxMessage" /> entity.
///     Implements <see cref="IEntityTypeConfiguration{TEntity}" /> to configure the entity properties and keys.
/// </summary>
public class OutboxMessageConfig : IEntityTypeConfiguration<Outbox_OutboxMessage>
{
    /// <summary>
    ///     Configures the <see cref="OutboxMessage" /> entity's properties and keys using the provided
    ///     <see cref="EntityTypeBuilder{TEntity}" />.
    /// </summary>
    /// <param name="builder">The <see cref="EntityTypeBuilder{OutboxMessage}" /> to configure the entity.</param>
    public void Configure(EntityTypeBuilder<Outbox_OutboxMessage> builder)
    {
        // Configures the primary key for the OutboxMessage entity
        builder.HasKey(e => e.Id);

        // Configures properties for the entity
        builder.Property(c => c.Data).IsRequired(); // Message payload (data)
        builder.Property(c => c.State).IsRequired(); // Current state of the message (e.g., ReadyToSend, Sent, etc.)
        builder.Property(c => c.Type).IsRequired().HasMaxLength(512); // Message type (event type)

        builder.Property(e => e.EventId).IsRequired(); // Unique identifier for the event

        builder.Property(c => c.EventDate); // Date when the event occurred
        builder.Property(c => c.ModifiedDate); // Date when the message was last modified
        builder.Property(c => c.RetryCount); // Failed publication attempts so far
        builder.Property(c => c.Error).HasMaxLength(2000); // Error from the most recent failed attempt
        builder.Property(c => c.ProcessedOn); // Date the message was successfully published

        // CreateOutboxMessage treats EventId as the idempotency key; enforce that in the schema.
        builder.HasIndex(e => e.EventId).IsUnique();

        // Covers the outbox job's "oldest ready-to-send first" query.
        builder.HasIndex(e => new { e.State, e.EventDate });
    }
}
