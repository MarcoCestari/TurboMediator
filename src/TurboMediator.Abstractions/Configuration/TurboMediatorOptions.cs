namespace TurboMediator;

/// <summary>
/// Configuration options for TurboMediator.
/// </summary>
public sealed class TurboMediatorOptions
{
    /// <summary>
    /// Gets or sets the notification publisher to use.
    /// Default is <see cref="ForeachAwaitPublisher"/>.
    /// </summary>
    public INotificationPublisher NotificationPublisher { get; set; } = ForeachAwaitPublisher.Instance;

    /// <summary>
    /// Gets or sets a value indicating whether to throw if no handler is registered for a notification.
    /// Default is false (notifications with no handlers are silently ignored).
    /// </summary>
    public bool ThrowOnNoNotificationHandler { get; set; } = false;
}
