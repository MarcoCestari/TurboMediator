using System;
using System.Collections.Generic;

namespace TurboMediator.Observability.Logging;

/// <summary>
/// Options for configuring structured logging behavior.
/// </summary>
public class StructuredLoggingOptions
{
    /// <summary>
    /// Gets or sets whether to include the message type in log entries.
    /// Default is true.
    /// </summary>
    public bool IncludeMessageType { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include the handler name in log entries.
    /// Default is true.
    /// </summary>
    public bool IncludeHandlerName { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include the execution duration in log entries.
    /// Default is true.
    /// </summary>
    public bool IncludeDuration { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include the correlation ID in log entries.
    /// Default is true.
    /// </summary>
    public bool IncludeCorrelationId { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include message properties in log entries.
    /// Default is false for performance reasons.
    /// </summary>
    public bool IncludeMessageProperties { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include the response in log entries.
    /// Default is false for performance reasons.
    /// </summary>
    public bool IncludeResponse { get; set; } = false;

    /// <summary>
    /// Gets or sets the property names that should be masked in log entries.
    /// Values will be replaced with "***REDACTED***".
    /// </summary>
    public ISet<string> SensitivePropertyNames { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Secret",
        "Token",
        "ApiKey",
        "ConnectionString",
        "CreditCard",
        "CardNumber",
        "Cvv",
        "Pin",
        "Ssn",
        "SocialSecurityNumber"
    };

    /// <summary>
    /// Gets or sets the log level for successful operations.
    /// Default is Information.
    /// </summary>
    public LogLevel SuccessLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Gets or sets the log level for failed operations.
    /// Default is Error.
    /// </summary>
    public LogLevel ErrorLogLevel { get; set; } = LogLevel.Error;

    /// <summary>
    /// Gets or sets the log level for slow operations.
    /// Default is Warning.
    /// </summary>
    public LogLevel SlowOperationLogLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// Gets or sets the threshold for slow operations.
    /// Operations taking longer than this will be logged at SlowOperationLogLevel.
    /// Default is 1 second.
    /// </summary>
    public TimeSpan SlowOperationThreshold { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets a predicate to determine if a message type should be logged.
    /// Return true to log, false to skip.
    /// </summary>
    public Func<Type, bool>? ShouldLog { get; set; }

    /// <summary>
    /// Gets or sets custom labels to add to all log entries.
    /// </summary>
    public IDictionary<string, string> CustomLabels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets whether to log the start of message handling.
    /// Default is false (only logs completion).
    /// </summary>
    public bool LogOnStart { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum length for serialized message/response in logs.
    /// Values longer than this will be truncated.
    /// Default is 1000 characters.
    /// </summary>
    public int MaxSerializedLength { get; set; } = 1000;
}

/// <summary>
/// Log levels for structured logging.
/// </summary>
public enum LogLevel
{
    /// <summary>Trace level logging.</summary>
    Trace = 0,
    /// <summary>Debug level logging.</summary>
    Debug = 1,
    /// <summary>Information level logging.</summary>
    Information = 2,
    /// <summary>Warning level logging.</summary>
    Warning = 3,
    /// <summary>Error level logging.</summary>
    Error = 4,
    /// <summary>Critical level logging.</summary>
    Critical = 5,
    /// <summary>No logging.</summary>
    None = 6
}
