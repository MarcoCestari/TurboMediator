using System;
using System.Collections.Generic;

namespace TurboMediator.Resilience.Fallback;

/// <summary>
/// Options for configuring fallback behavior.
/// </summary>
public class FallbackOptions
{
    /// <summary>
    /// Gets or sets the exception types that should trigger the fallback.
    /// If empty, all exceptions will trigger the fallback.
    /// </summary>
    public IList<Type> ExceptionTypes { get; set; } = new List<Type>();

    /// <summary>
    /// Gets or sets a predicate to determine if the fallback should be executed.
    /// If not specified, the fallback will be executed for any exception.
    /// </summary>
    public Func<Exception, bool>? ShouldHandle { get; set; }

    /// <summary>
    /// Gets or sets whether to throw the original exception if all fallbacks fail.
    /// Default is true.
    /// </summary>
    public bool ThrowOnAllFallbacksFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets the action to invoke when a fallback is triggered.
    /// Useful for logging or metrics.
    /// </summary>
    public Action<FallbackInvokedInfo>? OnFallbackInvoked { get; set; }

    /// <summary>
    /// Gets or sets the default value to return if all fallbacks fail and ThrowOnAllFallbacksFailure is false.
    /// </summary>
    public Func<object>? DefaultValueFactory { get; set; }
}

/// <summary>
/// Information about a fallback invocation.
/// </summary>
public readonly struct FallbackInvokedInfo
{
    /// <summary>
    /// Gets the type of message that triggered the fallback.
    /// </summary>
    public string MessageType { get; }

    /// <summary>
    /// Gets the exception that caused the fallback.
    /// </summary>
    public Exception Exception { get; }

    /// <summary>
    /// Gets the type of fallback handler being invoked.
    /// </summary>
    public Type FallbackHandlerType { get; }

    /// <summary>
    /// Gets the attempt number (1-based).
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Creates a new FallbackInvokedInfo.
    /// </summary>
    public FallbackInvokedInfo(
        string messageType,
        Exception exception,
        Type fallbackHandlerType,
        int attemptNumber)
    {
        MessageType = messageType;
        Exception = exception;
        FallbackHandlerType = fallbackHandlerType;
        AttemptNumber = attemptNumber;
    }
}
