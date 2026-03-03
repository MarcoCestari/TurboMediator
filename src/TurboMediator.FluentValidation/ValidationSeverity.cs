namespace TurboMediator.FluentValidation;

/// <summary>
/// Specifies the severity of a validation failure.
/// </summary>
public enum ValidationSeverity
{
    /// <summary>
    /// Error severity - validation has failed.
    /// </summary>
    Error,

    /// <summary>
    /// Warning severity - validation passed but with warnings.
    /// </summary>
    Warning,

    /// <summary>
    /// Info severity - informational message only.
    /// </summary>
    Info
}
