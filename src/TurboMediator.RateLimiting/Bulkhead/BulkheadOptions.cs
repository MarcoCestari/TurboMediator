using System;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Configuration options for bulkhead isolation behavior.
/// </summary>
public class BulkheadOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent executions.
    /// Default is 10.
    /// </summary>
    public int MaxConcurrent { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum number of requests that can wait in the queue.
    /// Default is 100.
    /// </summary>
    public int MaxQueue { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum time to wait in the queue.
    /// Null means infinite wait.
    /// </summary>
    public TimeSpan? QueueTimeout { get; set; }

    /// <summary>
    /// Gets or sets whether to throw an exception when the bulkhead is full.
    /// When false, returns default value for the response type.
    /// Default is true.
    /// </summary>
    public bool ThrowOnBulkheadFull { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to track metrics for monitoring.
    /// </summary>
    public bool TrackMetrics { get; set; }

    /// <summary>
    /// Gets or sets the action to execute when a request is rejected due to full bulkhead.
    /// </summary>
    public Action<BulkheadRejectionInfo>? OnRejection { get; set; }

    /// <summary>
    /// Gets or sets whether to use a separate bulkhead per partition key.
    /// When true, different users/tenants get their own isolation.
    /// </summary>
    public bool PerPartition { get; set; }

    /// <summary>
    /// Gets or sets the function to extract the partition key.
    /// </summary>
    public Func<string>? PartitionKeyProvider { get; set; }
}

/// <summary>
/// Information about a bulkhead rejection event.
/// </summary>
public class BulkheadRejectionInfo
{
    /// <summary>
    /// Gets the name of the message type that was rejected.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gets the current number of concurrent executions.
    /// </summary>
    public int CurrentConcurrency { get; }

    /// <summary>
    /// Gets the current queue depth.
    /// </summary>
    public int CurrentQueueDepth { get; }

    /// <summary>
    /// Gets the rejection reason.
    /// </summary>
    public BulkheadRejectionReason Reason { get; }

    /// <summary>
    /// Creates a new BulkheadRejectionInfo.
    /// </summary>
    public BulkheadRejectionInfo(
        string messageType,
        int currentConcurrency,
        int currentQueueDepth,
        BulkheadRejectionReason reason)
    {
        MessageType = messageType;
        CurrentConcurrency = currentConcurrency;
        CurrentQueueDepth = currentQueueDepth;
        Reason = reason;
    }
}

/// <summary>
/// Reason for bulkhead rejection.
/// </summary>
public enum BulkheadRejectionReason
{
    /// <summary>
    /// Both the execution slots and queue are full.
    /// </summary>
    BulkheadFull,

    /// <summary>
    /// Timed out waiting in the queue.
    /// </summary>
    QueueTimeout
}
