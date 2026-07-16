using System.Text.Json;
using Meadow_Framework.Core.Abstractions.Events;
using Meadow_Framework.Core.Abstractions.Primitives;

namespace Meadow_Framework.Core.Abstractions.Outbox;

/// <summary>
///     Represents an event awaiting publication to the message broker, stored in the same
///     transaction as the state change that produced it (transactional outbox pattern).
/// </summary>
public sealed class OutboxMessage : AggregateRoot<Guid>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.General);

    // Private constructor for EF Core or other serialization mechanisms
    private OutboxMessage() : base(Guid.NewGuid())
    {
        Data = string.Empty;
        Type = string.Empty;
    }

    /// <summary>
    ///     Creates a new instance of the <see cref="OutboxMessage" /> class.
    /// </summary>
    /// <param name="message">The event to be stored in the outbox.</param>
    /// <param name="eventId">The identifier for the event.</param>
    /// <param name="eventDate">The date when the event occurred.</param>
    public OutboxMessage(IEvent message, Guid eventId, DateTime eventDate) : base(Guid.NewGuid())
    {
        ArgumentNullException.ThrowIfNull(message);

        // Serialize against the runtime type: the static type here is IEvent, and serializing
        // against it would emit an empty payload.
        Data = JsonSerializer.Serialize(message, message.GetType(), SerializerOptions);

        // Store the type of the message for deserialization later
        Type = BuildTypeName(message.GetType());

        // Initialize properties
        EventId = eventId;
        EventDate = eventDate;
        State = OutboxMessageState.ReadyToSend;
        ModifiedDate = DateTime.UtcNow;
    }

    /// <summary>
    ///     Gets the serialized message data.
    /// </summary>
    public string Data { get; private set; }

    /// <summary>
    ///     Gets the full name and assembly name of the message type.
    /// </summary>
    public string Type { get; private set; }

    /// <summary>
    ///     Gets the unique identifier for the event associated with the message.
    /// </summary>
    public Guid EventId { get; private set; }

    /// <summary>
    ///     Gets the date when the event occurred.
    /// </summary>
    public DateTime EventDate { get; private set; }

    /// <summary>
    ///     Gets the state of the outbox message.
    /// </summary>
    public OutboxMessageState State { get; private set; }

    /// <summary>
    ///     Gets the date when the outbox message was last modified.
    /// </summary>
    public DateTime ModifiedDate { get; private set; }

    /// <summary>
    ///     Gets the number of failed publication attempts.
    /// </summary>
    public int RetryCount { get; private set; }

    /// <summary>
    ///     Gets the error from the most recent failed publication attempt, if any.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    ///     Gets the date the message was successfully published, if it has been.
    /// </summary>
    public DateTime? ProcessedOn { get; private set; }

    /// <summary>
    ///     Changes the state of the outbox message and updates the modification date.
    /// </summary>
    /// <param name="state">The new state of the outbox message.</param>
    public void ChangeState(OutboxMessageState state)
    {
        State = state;
        ModifiedDate = DateTime.UtcNow;

        if (state == OutboxMessageState.Completed)
        {
            ProcessedOn = ModifiedDate;
            Error = null;
        }
    }

    /// <summary>
    ///     Records a failed publication attempt. Once <paramref name="maxRetries" /> attempts have failed the
    ///     message moves to <see cref="OutboxMessageState.Failed" /> and is no longer picked up for sending.
    /// </summary>
    /// <param name="error">The error that caused the attempt to fail.</param>
    /// <param name="maxRetries">The number of attempts allowed before the message is considered poisoned.</param>
    public void RecordFailure(string error, int maxRetries)
    {
        RetryCount++;
        Error = error.Length > 2000 ? error[..2000] : error;
        State = RetryCount >= maxRetries ? OutboxMessageState.Failed : OutboxMessageState.ReadyToSend;
        ModifiedDate = DateTime.UtcNow;
    }

    /// <summary>
    ///     Recreates the original event from the stored data.
    /// </summary>
    /// <returns>The deserialized event.</returns>
    /// <exception cref="InvalidOperationException">
    ///     The stored type cannot be resolved, or does not describe an <see cref="IEvent" />.
    /// </exception>
    public IEvent RecreateMessage()
    {
        var messageType = System.Type.GetType(Type);

        if (messageType is null)
            throw new InvalidOperationException(
                $"Outbox message '{Id}' refers to type '{Type}', which cannot be resolved in this application.");

        // The type name comes from the database. Refuse anything that is not an event rather than
        // letting a tampered row drive construction of an arbitrary type.
        if (!typeof(IEvent).IsAssignableFrom(messageType))
            throw new InvalidOperationException(
                $"Outbox message '{Id}' refers to type '{Type}', which is not an {nameof(IEvent)}.");

        return (IEvent)JsonSerializer.Deserialize(Data, messageType, SerializerOptions)!;
    }

    private static string BuildTypeName(System.Type type)
    {
        return $"{type.FullName}, {type.Assembly.GetName().Name}";
    }
}
