namespace TurboMediator.Scheduling;

/// <summary>
/// Thrown by a job handler to terminate execution with a specific status, without triggering a retry.
/// </summary>
public sealed class TerminateJobException : Exception
{
    /// <summary>The desired final status for the occurrence.</summary>
    public JobStatus TerminalStatus { get; }

    /// <summary>Creates a new TerminateJobException.</summary>
    /// <param name="status">The desired final status (e.g., Failed, Cancelled).</param>
    /// <param name="reason">The reason for termination.</param>
    public TerminateJobException(JobStatus status, string reason) : base(reason)
    {
        TerminalStatus = status;
    }

    /// <summary>Creates a new TerminateJobException.</summary>
    /// <param name="status">The desired final status.</param>
    /// <param name="reason">The reason for termination.</param>
    /// <param name="innerException">Inner exception.</param>
    public TerminateJobException(JobStatus status, string reason, Exception innerException) : base(reason, innerException)
    {
        TerminalStatus = status;
    }
}
