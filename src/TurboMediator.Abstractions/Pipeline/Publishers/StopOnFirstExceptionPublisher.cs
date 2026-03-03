namespace TurboMediator;

/// <summary>
/// Publishes notifications sequentially but stops on first exception.
/// </summary>
public sealed class StopOnFirstExceptionPublisher : INotificationPublisher
{
    /// <summary>
    /// Gets the singleton instance of the publisher.
    /// </summary>
    public static StopOnFirstExceptionPublisher Instance { get; } = new();

    private StopOnFirstExceptionPublisher() { }

    /// <inheritdoc />
    public async ValueTask Publish<TNotification>(
        IEnumerable<INotificationHandler<TNotification>> handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        // Exceptions will naturally propagate and stop execution
        foreach (var handler in handlers)
        {
            await handler.Handle(notification, cancellationToken);
        }
    }
}
