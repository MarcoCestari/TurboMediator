namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Interface for storing and retrieving outbox messages.
/// Implement this for your data access technology (EF Core, Dapper, ADO.NET, etc.).
/// </summary>
public interface IOutboxStore
{
    /// <summary>
    /// Saves a message to the outbox.
    /// </summary>
    ValueTask SaveAsync(OutboxMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets pending messages for processing.
    /// </summary>
    IAsyncEnumerable<OutboxMessage> GetPendingAsync(int batchSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as currently being processed.
    /// </summary>
    ValueTask MarkAsProcessingAsync(System.Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to atomically claim a message for processing by a specific worker.
    /// Uses optimistic concurrency: only one worker can claim a message.
    /// Returns true if the claim was successful (this worker won), false if another worker already claimed it.
    /// </summary>
    /// <param name="messageId">The message to claim.</param>
    /// <param name="workerId">The identifier of the worker attempting to claim.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if claimed successfully, false if already claimed by another worker.</returns>
    ValueTask<bool> TryClaimAsync(System.Guid messageId, string workerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a message as successfully processed.
    /// </summary>
    ValueTask MarkAsProcessedAsync(System.Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a processing failure: increments retry count, records the error,
    /// and resets the message to Pending so it can be retried by the background processor.
    /// </summary>
    ValueTask IncrementRetryAsync(System.Guid messageId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a message to dead letter status after exceeding max retries.
    /// </summary>
    ValueTask MoveToDeadLetterAsync(System.Guid messageId, string reason, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes processed messages older than the specified age.
    /// </summary>
    ValueTask<int> CleanupAsync(System.TimeSpan olderThan, CancellationToken cancellationToken = default);
}
