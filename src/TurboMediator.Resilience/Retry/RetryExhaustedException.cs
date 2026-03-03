using System;
using System.Collections.Generic;
using System.Linq;

namespace TurboMediator.Resilience.Retry;

/// <summary>
/// Exception thrown when all retry attempts have been exhausted.
/// </summary>
public class RetryExhaustedException : Exception
{
    /// <summary>
    /// Gets the list of exceptions from each failed attempt.
    /// </summary>
    public IReadOnlyList<Exception> Exceptions { get; }

    /// <summary>
    /// Creates a new RetryExhaustedException.
    /// </summary>
    public RetryExhaustedException(string message, IEnumerable<Exception> exceptions)
        : base(message, exceptions.LastOrDefault())
    {
        Exceptions = exceptions.ToList().AsReadOnly();
    }
}
