namespace TurboMediator.Results;

/// <summary>
/// Exception type used by Result when creating failures from error messages.
/// </summary>
public class ResultException : Exception
{
    /// <summary>
    /// Creates a new ResultException with the specified message.
    /// </summary>
    public ResultException(string message) : base(message) { }

    /// <summary>
    /// Creates a new ResultException with the specified message and inner exception.
    /// </summary>
    public ResultException(string message, Exception innerException) : base(message, innerException) { }
}
