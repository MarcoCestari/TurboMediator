using System;

namespace TurboMediator.Resilience.Hedging;

/// <summary>
/// Options for configuring hedging behavior.
/// </summary>
public class HedgingOptions
{
    /// <summary>
    /// Gets or sets the maximum number of parallel attempts.
    /// Default is 2.
    /// </summary>
    public int MaxParallelAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets the delay before starting additional parallel attempts.
    /// Default is 100ms.
    /// </summary>
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets a predicate to determine if the exception should trigger another hedged request.
    /// If not specified, all exceptions will trigger hedged requests.
    /// </summary>
    public Func<Exception, bool>? ShouldHandle { get; set; }

    /// <summary>
    /// Gets or sets an action to be called when a hedged request is started.
    /// </summary>
    public Action<HedgingAttemptInfo>? OnHedgingAttempt { get; set; }

    /// <summary>
    /// Gets or sets whether to cancel other pending requests when one succeeds.
    /// Default is true.
    /// </summary>
    public bool CancelPendingOnSuccess { get; set; } = true;
}

/// <summary>
/// Information about a hedging attempt.
/// </summary>
public readonly struct HedgingAttemptInfo
{
    /// <summary>
    /// Gets the type of message being handled.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gets the attempt number (1-based).
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the total number of parallel attempts.
    /// </summary>
    public int TotalAttempts { get; }

    /// <summary>
    /// Gets the exception from the previous attempt, if any.
    /// </summary>
    public Exception? PreviousException { get; }

    /// <summary>
    /// Creates a new HedgingAttemptInfo.
    /// </summary>
    public HedgingAttemptInfo(
        string messageType,
        int attemptNumber,
        int totalAttempts,
        Exception? previousException)
    {
        MessageType = messageType;
        AttemptNumber = attemptNumber;
        TotalAttempts = totalAttempts;
        PreviousException = previousException;
    }
}
