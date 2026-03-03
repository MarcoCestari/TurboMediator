using System;

namespace TurboMediator.Resilience.Hedging;

/// <summary>
/// Attribute to enable hedging behavior for a message handler.
/// Hedging sends parallel requests and uses the first successful response.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public sealed class HedgingAttribute : Attribute
{
    /// <summary>
    /// Gets the maximum number of parallel attempts.
    /// </summary>
    public int MaxParallelAttempts { get; }

    /// <summary>
    /// Gets or sets the delay in milliseconds before starting additional parallel attempts.
    /// </summary>
    public int DelayMs { get; set; } = 100;

    /// <summary>
    /// Creates a new HedgingAttribute.
    /// </summary>
    /// <param name="maxParallelAttempts">The maximum number of parallel attempts. Must be at least 2.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when maxParallelAttempts is less than 2.</exception>
    public HedgingAttribute(int maxParallelAttempts = 2)
    {
        if (maxParallelAttempts < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(maxParallelAttempts), "Must be at least 2 for hedging to be useful.");
        }
        MaxParallelAttempts = maxParallelAttempts;
    }
}
