using System;

namespace TurboMediator.Testing;

/// <summary>
/// Exception thrown when a verification fails.
/// </summary>
public class VerificationException : Exception
{
    /// <summary>
    /// Creates a new VerificationException.
    /// </summary>
    public VerificationException(string message) : base(message) { }
}
