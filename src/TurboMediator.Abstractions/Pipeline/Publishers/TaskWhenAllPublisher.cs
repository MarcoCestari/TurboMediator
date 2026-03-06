namespace TurboMediator;

/// <summary>
/// Publishes notifications to all handlers in parallel using Task.WhenAll.
/// </summary>
public sealed class TaskWhenAllPublisher : INotificationPublisher
{
    /// <summary>
    /// Gets the singleton instance of the publisher.
    /// </summary>
    public static TaskWhenAllPublisher Instance { get; } = new();

    private TaskWhenAllPublisher() { }

    /// <inheritdoc />
    public async ValueTask Publish<TNotification>(
        INotificationHandler<TNotification>[] handlers,
        TNotification notification,
        CancellationToken cancellationToken)
        where TNotification : INotification
    {
        var tasks = new Task[handlers.Length];
        for (int i = 0; i < handlers.Length; i++)
        {
            tasks[i] = handlers[i].Handle(notification, cancellationToken).AsTask();
        }
        await Task.WhenAll(tasks);
    }
}
