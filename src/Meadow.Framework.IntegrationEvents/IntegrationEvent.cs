namespace Meadow.Framework.IntegrationEvents;

/// <summary>
///     Represents the base class for integration events.
///     Integration events are used to communicate across different systems or components.
/// </summary>
public abstract class IntegrationEvent
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="IntegrationEvent" /> class.
    /// </summary>
    /// <param name="id">The unique identifier of the event.</param>
    /// <param name="creationDate">The date and time when the event was created.</param>
    protected IntegrationEvent(Guid id, DateTime creationDate)
    {
        Id = id;
        CreationDate = creationDate;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="IntegrationEvent" /> class with default values.
    ///     This constructor generates a new unique identifier and sets the creation date to the current UTC time.
    /// </summary>
    protected IntegrationEvent() : this(Guid.NewGuid(), DateTime.UtcNow)
    {
    }

    /// <summary>
    ///     Gets the unique identifier of the event.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    ///     Gets the date and time when the event was created.
    /// </summary>
    public DateTime CreationDate { get; }
}