namespace TurboMediator.Enterprise.Deduplication;

/// <summary>
/// Interface for messages that support idempotency/deduplication.
/// </summary>
public interface IIdempotentMessage
{
    /// <summary>
    /// Gets the unique idempotency key for this message.
    /// </summary>
    string IdempotencyKey { get; }
}
