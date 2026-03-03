using System;

namespace TurboMediator.Batching;

/// <summary>
/// Options for configuring request batching behavior.
/// </summary>
public class BatchingOptions
{
    /// <summary>
    /// Gets or sets the maximum number of requests to batch together.
    /// Default is 100.
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the maximum time to wait for more requests before processing the batch.
    /// Default is 10ms.
    /// </summary>
    public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromMilliseconds(10);

    /// <summary>
    /// Gets or sets whether to throw if a batch handler is not found.
    /// If false, falls back to individual query execution.
    /// Default is false.
    /// </summary>
    public bool ThrowIfNoBatchHandler { get; set; }

    /// <summary>
    /// Gets or sets the action to invoke when a batch is processed.
    /// </summary>
    public Action<BatchProcessedInfo>? OnBatchProcessed { get; set; }
}

/// <summary>
/// Information about a processed batch.
/// </summary>
public readonly struct BatchProcessedInfo
{
    /// <summary>
    /// Gets the type of query that was batched.
    /// </summary>
    public Type QueryType { get; }

    /// <summary>
    /// Gets the number of queries in the batch.
    /// </summary>
    public int BatchSize { get; }

    /// <summary>
    /// Gets the time taken to process the batch.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Creates a new BatchProcessedInfo.
    /// </summary>
    public BatchProcessedInfo(Type queryType, int batchSize, TimeSpan duration)
    {
        QueryType = queryType;
        BatchSize = batchSize;
        Duration = duration;
    }
}
