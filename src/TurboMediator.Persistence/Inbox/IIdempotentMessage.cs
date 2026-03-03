namespace TurboMediator.Persistence.Inbox;

/// <summary>
/// Interface for messages that provide an explicit idempotency key.
/// Implementing this interface is the preferred way to enable inbox deduplication.
/// </summary>
public interface IIdempotentMessage
{
    /// <summary>
    /// Gets the unique idempotency key for this message.
    /// Messages with the same key are considered duplicates and processed at most once.
    /// </summary>
    string IdempotencyKey { get; }
}
