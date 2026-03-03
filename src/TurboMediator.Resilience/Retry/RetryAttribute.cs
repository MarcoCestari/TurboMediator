using System;

namespace TurboMediator.Resilience.Retry;

/// <summary>
/// Specifies retry policy for a message handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class RetryAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; }

    /// <summary>
    /// Gets the initial delay in milliseconds between retries.
    /// </summary>
    public int DelayMilliseconds { get; }

    /// <summary>
    /// Gets whether to use exponential backoff. Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets the maximum delay in milliseconds between retries. Default is 30000ms (30 seconds).
    /// </summary>
    public int MaxDelayMilliseconds { get; set; } = 30000;

    /// <summary>
    /// Gets or sets the types of exceptions that should trigger a retry.
    /// If empty, all exceptions will trigger a retry.
    /// </summary>
    public Type[]? RetryOnExceptions { get; set; }

    /// <summary>
    /// Creates a new RetryAttribute with the specified retry policy.
    /// </summary>
    /// <param name="maxAttempts">The maximum number of retry attempts (including the initial attempt).</param>
    /// <param name="delayMilliseconds">The initial delay in milliseconds between retries.</param>
    public RetryAttribute(int maxAttempts, int delayMilliseconds = 1000)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts), "Max attempts must be at least 1.");
        if (delayMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(delayMilliseconds), "Delay must be non-negative.");

        MaxAttempts = maxAttempts;
        DelayMilliseconds = delayMilliseconds;
    }
}
