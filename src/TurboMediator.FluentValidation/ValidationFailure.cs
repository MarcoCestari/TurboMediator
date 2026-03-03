using System;
using System.Collections.Generic;

namespace TurboMediator.FluentValidation;

/// <summary>
/// Represents a validation failure from FluentValidation.
/// </summary>
public class ValidationFailure
{
    /// <summary>
    /// Gets or sets the property name that failed validation.
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message.
    /// </summary>
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the attempted value that failed validation.
    /// </summary>
    public object? AttemptedValue { get; set; }

    /// <summary>
    /// Gets or sets the error code.
    /// </summary>
    public string? ErrorCode { get; set; }

    /// <summary>
    /// Gets or sets the severity of the failure.
    /// </summary>
    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    /// <summary>
    /// Creates a new validation failure.
    /// </summary>
    public ValidationFailure()
    {
    }

    /// <summary>
    /// Creates a new validation failure with property name and error message.
    /// </summary>
    /// <param name="propertyName">The property name that failed validation.</param>
    /// <param name="errorMessage">The error message.</param>
    public ValidationFailure(string propertyName, string errorMessage)
    {
        PropertyName = propertyName;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a new validation failure with property name, error message, and attempted value.
    /// </summary>
    /// <param name="propertyName">The property name that failed validation.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="attemptedValue">The value that was attempted.</param>
    public ValidationFailure(string propertyName, string errorMessage, object? attemptedValue)
    {
        PropertyName = propertyName;
        ErrorMessage = errorMessage;
        AttemptedValue = attemptedValue;
    }
}
