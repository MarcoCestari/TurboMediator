using System;

namespace TurboMediator.Validation;

/// <summary>
/// Represents a single validation error.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets the name of the property that failed validation.
    /// </summary>
    public string PropertyName { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string ErrorMessage { get; }

    /// <summary>
    /// Gets the attempted value that failed validation.
    /// </summary>
    public object? AttemptedValue { get; }

    /// <summary>
    /// Gets the error code for this validation error.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Creates a new validation error.
    /// </summary>
    /// <param name="propertyName">The name of the property that failed validation.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="attemptedValue">The attempted value that failed validation.</param>
    /// <param name="errorCode">The error code for this validation error.</param>
    public ValidationError(string propertyName, string errorMessage, object? attemptedValue = null, string? errorCode = null)
    {
        PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
        ErrorMessage = errorMessage ?? throw new ArgumentNullException(nameof(errorMessage));
        AttemptedValue = attemptedValue;
        ErrorCode = errorCode;
    }

    /// <inheritdoc />
    public override string ToString() => $"{PropertyName}: {ErrorMessage}";
}
