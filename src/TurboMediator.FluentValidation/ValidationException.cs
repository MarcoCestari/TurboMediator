using System;
using System.Collections.Generic;

namespace TurboMediator.FluentValidation;

/// <summary>
/// Exception thrown when validation fails.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// Gets the validation failures that caused this exception.
    /// </summary>
    public IReadOnlyList<ValidationFailure> Failures { get; }

    /// <summary>
    /// Creates a new ValidationException with the specified failures.
    /// </summary>
    /// <param name="failures">The validation failures.</param>
    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base(BuildErrorMessage(failures))
    {
        Failures = failures as IReadOnlyList<ValidationFailure> ?? new List<ValidationFailure>(failures);
    }

    /// <summary>
    /// Creates a new ValidationException with a single failure.
    /// </summary>
    /// <param name="propertyName">The property name that failed.</param>
    /// <param name="errorMessage">The error message.</param>
    public ValidationException(string propertyName, string errorMessage)
        : this(new[] { new ValidationFailure(propertyName, errorMessage) })
    {
    }

    private static string BuildErrorMessage(IEnumerable<ValidationFailure> failures)
    {
        var messages = new List<string>();
        foreach (var failure in failures)
        {
            messages.Add($"{failure.PropertyName}: {failure.ErrorMessage}");
        }
        return $"Validation failed: {string.Join("; ", messages)}";
    }
}
