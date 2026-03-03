namespace TurboMediator.Validation;

/// <summary>
/// Options for configuring validation behavior.
/// </summary>
public class ValidationBehaviorOptions
{
    /// <summary>
    /// Gets or sets whether to stop validation on the first failure.
    /// Default is false (collect all errors).
    /// </summary>
    public bool StopOnFirstFailure { get; set; } = false;
}
