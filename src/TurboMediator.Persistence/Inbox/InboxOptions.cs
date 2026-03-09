namespace TurboMediator.Persistence.Inbox;

/// <summary>
/// Options for configuring the inbox behavior.
/// </summary>
public class InboxOptions
{
    /// <summary>
    /// Gets or sets how long to keep inbox records before cleanup. Default is 7 days.
    /// </summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Gets or sets whether to enable automatic cleanup of old inbox records. Default is true.
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between cleanup runs. Default is 1 hour.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
}
