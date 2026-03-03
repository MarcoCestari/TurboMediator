namespace TurboMediator;

/// <summary>
/// Publishes notifications to all handlers in parallel but does not wait for completion (fire-and-forget).
/// Use with caution - exceptions will be silently ignored.
/// </summary>
public sealed class FireAndForgetPublisher : INotificationPublisher
{
    /// <summary>
    /// Gets the singleton instance of the publisher.
    /// </summary>
    public static FireAndForgetPublisher Instance { get; } = new();

    private FireAndForgetPublisher() { }

    /// <inheritdoc />
    public ValueTask Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        foreach (var handler in handlers)
        {
            // Fire and forget - don't await
            _ = Task.Run(async () =>
            {
                try
                {
                    await handler.Handle(notification, cancellationToken);
                }
                catch
                {
                    // Silently ignore exceptions in fire-and-forget mode
                }
            }, cancellationToken);
        }

        return default;
    }
}
