using System;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Applies bulkhead isolation to a message handler, limiting concurrent executions.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class BulkheadAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of concurrent executions.
    /// </summary>
    public int MaxConcurrent { get; }

    /// <summary>
    /// Gets the maximum number of requests that can wait in the queue.
    /// </summary>
    public int MaxQueue { get; }

    /// <summary>
    /// Gets or sets the maximum time to wait in the queue in milliseconds.
    /// Zero means infinite wait. Default is 0.
    /// </summary>
    public int QueueTimeoutMs { get; set; }

    /// <summary>
    /// Creates a new BulkheadAttribute with the specified limits.
    /// </summary>
    /// <param name="maxConcurrent">Maximum concurrent executions.</param>
    /// <param name="maxQueue">Maximum queue size. Default is 0 (no queue).</param>
    public BulkheadAttribute(int maxConcurrent, int maxQueue = 0)
    {
        if (maxConcurrent <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxConcurrent), "MaxConcurrent must be greater than zero.");
        if (maxQueue < 0)
            throw new ArgumentOutOfRangeException(nameof(maxQueue), "MaxQueue cannot be negative.");

        MaxConcurrent = maxConcurrent;
        MaxQueue = maxQueue;
    }
}
