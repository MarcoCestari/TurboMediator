using System;
using System.Collections.Generic;

namespace TurboMediator.Validation;

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets the collection of validation errors.
    /// </summary>
    public IReadOnlyList<ValidationError> Errors { get; }

    /// <summary>
    /// Gets a value indicating whether the validation was successful.
    /// </summary>
    public bool IsValid => Errors.Count == 0;

    /// <summary>
    /// Creates a new validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    public ValidationResult(IReadOnlyList<ValidationError>? errors = null)
    {
        Errors = errors ?? Array.Empty<ValidationError>();
    }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new();

    /// <summary>
    /// Creates a failed validation result with the specified errors.
    /// </summary>
    public static ValidationResult Failure(params ValidationError[] errors) => new(errors);

    /// <summary>
    /// Creates a failed validation result with a single error.
    /// </summary>
    public static ValidationResult Failure(string propertyName, string errorMessage)
        => new(new[] { new ValidationError(propertyName, errorMessage) });
}
