namespace TurboMediator.Scheduling;

/// <summary>
/// Thrown by a job handler to skip the current occurrence without triggering a retry.
/// The occurrence will be recorded with <see cref="JobStatus.Skipped"/>.
/// </summary>
public sealed class SkipJobException : Exception
{
    /// <summary>Creates a new SkipJobException.</summary>
    /// <param name="reason">The reason for skipping.</param>
    public SkipJobException(string reason) : base(reason) { }

    /// <summary>Creates a new SkipJobException.</summary>
    /// <param name="reason">The reason for skipping.</param>
    /// <param name="innerException">Inner exception.</param>
    public SkipJobException(string reason, Exception innerException) : base(reason, innerException) { }
}
