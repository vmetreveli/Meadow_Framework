namespace Meadow.Framework.Outbox;

/// <summary>
///     Configuration options for the Meadow Framework Outbox.
/// </summary>
public class OutboxOptions
{
    /// <summary>
    ///     Gets or sets the cron schedule for processing outbox messages.
    ///     Default is "0/30 * * * * ?" (every 30 seconds).
    /// </summary>
    public string ProcessingSchedule { get; set; } = "0/30 * * * * ?";

    /// <summary>
    ///     Gets or sets the maximum number of retry attempts for failed message publishing.
    ///     Default is 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    ///     Gets or sets the delay between retry attempts in seconds.
    ///     Default is 60 seconds.
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 60;

    /// <summary>
    ///     Gets or sets the batch size for processing outbox messages.
    ///     Default is 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    ///     Gets or sets whether to enable background processing of outbox messages.
    ///     Default is true.
    /// </summary>
    public bool EnableBackgroundProcessing { get; set; } = true;
}