using System;
using System.Collections.Generic;
using System.Linq;

namespace TurboMediator.Validation;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Gets the type of the message that failed validation.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Creates a new validation exception.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="messageType">The type of the message that failed validation.</param>
    public ValidationException(IReadOnlyList<ValidationError> errors, Type messageType)
        : base(FormatMessage(errors, messageType))
    {
        Errors = errors ?? throw new ArgumentNullException(nameof(errors));
        MessageType = messageType ?? throw new ArgumentNullException(nameof(messageType));
    }

    private static string FormatMessage(IReadOnlyList<ValidationError> errors, Type messageType)
    {
        if (errors == null || errors.Count == 0)
            return $"Validation failed for {messageType.Name}.";

        var errorMessages = string.Join("; ", errors.Select(e => e.ToString()));
        return $"Validation failed for {messageType.Name}: {errorMessages}";
    }
}
