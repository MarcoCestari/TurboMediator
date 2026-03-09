namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Handles outbox messages that have exceeded their maximum retry attempts.
/// Implement this to move failed messages to a dead letter queue, send alerts, or take compensating actions.
/// </summary>
public interface IOutboxDeadLetterHandler
{
    /// <summary>
    /// Handles a dead-lettered message.
    /// Called when a message exceeds its configured maximum retry attempts.
    /// </summary>
    /// <param name="message">The failed outbox message.</param>
    /// <param name="reason">The reason the message was dead-lettered.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask HandleAsync(OutboxMessage message, string reason, CancellationToken cancellationToken = default);
}
