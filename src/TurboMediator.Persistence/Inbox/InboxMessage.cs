namespace TurboMediator.Persistence.Inbox;

/// <summary>
/// Represents a processed message in the inbox for idempotency/deduplication.
/// Used to ensure at-most-once processing of messages from external sources.
/// </summary>
public class InboxMessage
{
    /// <summary>
    /// Gets or sets the unique message identifier (idempotency key).
    /// This could be a broker message ID, a business-defined key, or a hash.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full type name of the handler that processed this message.
    /// Allows the same message to be processed by multiple handlers independently.
    /// </summary>
    public string HandlerType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the full type name of the message.
    /// </summary>
    public string MessageType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when the message was first received.
    /// </summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was successfully processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }
}
