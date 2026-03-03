using System;
using System.Linq;

namespace TurboMediator.Scheduling;

/// <summary>
/// Defines retry behavior with customizable intervals per attempt.
/// </summary>
public sealed class RetryStrategy
{
    /// <summary>Retry intervals in seconds, one per attempt.</summary>
    public int[] IntervalSeconds { get; }

    /// <summary>Maximum number of retry attempts (equal to the length of intervals).</summary>
    public int MaxAttempts => IntervalSeconds.Length;

    private RetryStrategy(int[] intervalSeconds)
    {
        IntervalSeconds = intervalSeconds ?? throw new ArgumentNullException(nameof(intervalSeconds));
    }

    /// <summary>No retries.</summary>
    public static RetryStrategy None => new(Array.Empty<int>());

    /// <summary>
    /// Custom intervals per attempt.
    /// E.g., [30, 60, 300, 900] retries after 30s, 60s, 5min, 15min.
    /// </summary>
    public static RetryStrategy Custom(params int[] intervalSeconds)
        => new(intervalSeconds);

    /// <summary>
    /// Fixed delay between all retry attempts.
    /// </summary>
    public static RetryStrategy Fixed(int attempts, int delaySeconds)
        => new(Enumerable.Repeat(delaySeconds, attempts).ToArray());

    /// <summary>
    /// Exponential backoff: 1min, 2min, 4min, 8min, ...
    /// </summary>
    public static RetryStrategy ExponentialBackoff(int maxAttempts, int baseSeconds = 60)
        => new(Enumerable.Range(0, maxAttempts)
            .Select(i => baseSeconds * (int)Math.Pow(2, i))
            .ToArray());

    /// <summary>
    /// Immediate retries (0 delay between attempts).
    /// </summary>
    public static RetryStrategy Immediate(int attempts)
        => new(Enumerable.Repeat(0, attempts).ToArray());
}
