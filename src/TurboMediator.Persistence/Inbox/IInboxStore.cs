namespace TurboMediator.Persistence.Inbox;

/// <summary>
/// Interface for storing and checking inbox message deduplication records.
/// Implement this for your data access technology (EF Core, Dapper, ADO.NET, etc.).
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// Checks whether a message has already been processed by a specific handler.
    /// </summary>
    /// <param name="messageId">The unique message identifier (idempotency key).</param>
    /// <param name="handlerType">The handler type name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message has already been processed by this handler.</returns>
    ValueTask<bool> HasBeenProcessedAsync(string messageId, string handlerType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a message as processed by a specific handler.
    /// Should use INSERT with conflict handling (UPSERT) for concurrency safety.
    /// </summary>
    /// <param name="message">The inbox message to record.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask RecordAsync(InboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes inbox records older than the specified age.
    /// </summary>
    /// <param name="olderThan">The age threshold for cleanup.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of records deleted.</returns>
    ValueTask<int> CleanupAsync(TimeSpan olderThan, CancellationToken cancellationToken = default);
}
