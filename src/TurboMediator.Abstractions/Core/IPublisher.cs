namespace TurboMediator;

/// <summary>
/// Defines a publisher for notifications.
/// </summary>
public interface IPublisher
{
    /// <summary>
    /// Publishes a notification to multiple handlers.
    /// </summary>
    /// <typeparam name="TNotification">The type of notification.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
