using System;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Options for the outbox processor.
/// </summary>
public class OutboxProcessorOptions
{
    /// <summary>
    /// Interval between processing batches. Default: 5 seconds.
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Number of messages to process per batch. Default: 100.
    /// </summary>
    public int BatchSize { get; set; } = 100;

    /// <summary>
    /// Maximum retry attempts before moving to dead letter. Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Time to wait before retrying failed messages. Default: 30 seconds.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Whether to publish to external message broker. Default: false.
    /// </summary>
    public bool PublishToMessageBroker { get; set; } = false;

    /// <summary>
    /// Cleanup processed messages older than this duration. Default: 7 days.
    /// </summary>
    public TimeSpan CleanupAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Whether to run cleanup automatically. Default: true.
    /// </summary>
    public bool EnableAutoCleanup { get; set; } = true;

    /// <summary>
    /// Interval between cleanup runs. Default: 1 hour.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Unique identifier for this worker instance. Used for optimistic concurrency when
    /// multiple workers process the outbox simultaneously. Default: auto-generated GUID.
    /// </summary>
    public string WorkerId { get; set; } = Guid.NewGuid().ToString("N")[..8];
}
