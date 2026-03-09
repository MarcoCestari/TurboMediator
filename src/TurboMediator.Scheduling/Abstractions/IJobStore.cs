namespace TurboMediator.Scheduling;

/// <summary>
/// Persists recurring job definitions and their execution occurrences.
/// </summary>
public interface IJobStore
{
    // --- Job definitions ---

    /// <summary>Creates or updates a recurring job record (upsert).</summary>
    Task UpsertJobAsync(RecurringJobRecord job, CancellationToken ct = default);

    /// <summary>Gets a job by its unique ID.</summary>
    Task<RecurringJobRecord?> GetJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>Gets all registered jobs.</summary>
    Task<IReadOnlyList<RecurringJobRecord>> GetAllJobsAsync(CancellationToken ct = default);

    /// <summary>Gets jobs that are due for execution (NextRunAt &lt;= now and not Paused).</summary>
    Task<IReadOnlyList<RecurringJobRecord>> GetDueJobsAsync(DateTimeOffset now, CancellationToken ct = default);

    /// <summary>Removes a job and all its occurrences.</summary>
    Task<bool> RemoveJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Attempts to acquire a lock on the job for execution (optimistic concurrency).
    /// Returns true if the job was successfully marked as Running.
    /// </summary>
    Task<bool> TryLockJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>Releases the lock on a job after execution completes.</summary>
    Task ReleaseJobAsync(string jobId, JobStatus newStatus, DateTimeOffset? nextRunAt, CancellationToken ct = default);

    // --- Occurrences ---

    /// <summary>Records a new job occurrence.</summary>
    Task AddOccurrenceAsync(JobOccurrenceRecord occurrence, CancellationToken ct = default);

    /// <summary>Updates an existing occurrence record.</summary>
    Task UpdateOccurrenceAsync(JobOccurrenceRecord occurrence, CancellationToken ct = default);

    /// <summary>
    /// Gets the most recent occurrences for a job, ordered by StartedAt descending.
    /// </summary>
    Task<IReadOnlyList<JobOccurrenceRecord>> GetOccurrencesAsync(string jobId, int limit = 20, CancellationToken ct = default);
}
