namespace Meadow.Framework.IntegrationEvents;

/// <summary>
///     Represents the possible states of an integration event.
/// </summary>
public enum IntegrationEventStatus
{
    /// <summary>
    ///     The event is pending and has not been published yet.
    /// </summary>
    Pending = 1,

    /// <summary>
    ///     The event has been published successfully.
    /// </summary>
    Published = 2,

    /// <summary>
    ///     The event failed to publish.
    /// </summary>
    Failed = 3
}