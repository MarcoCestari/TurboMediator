namespace TurboMediator.Resilience.Timeout;

/// <summary>
/// Specifies a timeout for a message handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class TimeoutAttribute : Attribute
{
    /// <summary>
    /// Gets the timeout in milliseconds.
    /// </summary>
    public int Milliseconds { get; }

    /// <summary>
    /// Gets the timeout as a TimeSpan.
    /// </summary>
    public TimeSpan Timeout => TimeSpan.FromMilliseconds(Milliseconds);

    /// <summary>
    /// Creates a new TimeoutAttribute with the specified timeout in milliseconds.
    /// </summary>
    /// <param name="milliseconds">The timeout in milliseconds.</param>
    public TimeoutAttribute(int milliseconds)
    {
        if (milliseconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(milliseconds), "Timeout must be greater than zero.");

        Milliseconds = milliseconds;
    }
}
