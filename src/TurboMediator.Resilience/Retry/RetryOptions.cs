using System;

namespace TurboMediator.Resilience.Retry;

/// <summary>
/// Options for configuring retry behavior.
/// </summary>
public class RetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay in milliseconds between retries. Default is 1000ms.
    /// </summary>
    public int DelayMilliseconds { get; set; } = 1000;

    /// <summary>
    /// Gets or sets whether to use exponential backoff. Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum delay in milliseconds between retries. Default is 30000ms.
    /// </summary>
    public int MaxDelayMilliseconds { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the types of exceptions that should trigger a retry.
    /// If null or empty, all exceptions (except OperationCanceledException) will trigger a retry.
    /// </summary>
    public Type[]? RetryOnExceptions { get; set; }
}
