using System.Collections.Concurrent;

namespace TurboMediator.Scheduling.Internal;

/// <summary>
/// In-memory implementation of <see cref="IJobStore"/> for development and testing.
/// Thread-safe but not suitable for multi-instance deployments.
/// </summary>
internal sealed class InMemoryJobStore : IJobStore
{
    private readonly ConcurrentDictionary<string, RecurringJobRecord> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, List<JobOccurrenceRecord>> _occurrences = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Task UpsertJobAsync(RecurringJobRecord job, CancellationToken ct = default)
    {
        _jobs.AddOrUpdate(job.JobId, job, (_, existing) =>
        {
            // Upsert: update fields but preserve runtime state if job already exists
            existing.CronExpression = job.CronExpression;
            existing.Interval = job.Interval;
            existing.RetryIntervalSeconds = job.RetryIntervalSeconds;
            existing.SkipIfAlreadyRunning = job.SkipIfAlreadyRunning;
            existing.Priority = job.Priority;
            existing.MessageTypeName = job.MessageTypeName;
            existing.MessagePayload = job.MessagePayload;
            existing.TimeZoneId = job.TimeZoneId;
            existing.UpdatedAt = DateTimeOffset.UtcNow;

            // Recalculate next run if not yet set
            if (existing.NextRunAt == null)
                existing.NextRunAt = job.NextRunAt;

            return existing;
        });
        return Task.CompletedTask;
    }

    public Task<RecurringJobRecord?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<IReadOnlyList<RecurringJobRecord>> GetAllJobsAsync(CancellationToken ct = default)
    {
        IReadOnlyList<RecurringJobRecord> result = _jobs.Values.ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<RecurringJobRecord>> GetDueJobsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        IReadOnlyList<RecurringJobRecord> result = _jobs.Values
            .Where(j => j.Status != JobStatus.Paused
                        && j.Status != JobStatus.Running
                        && j.NextRunAt.HasValue
                        && j.NextRunAt.Value <= now)
            .OrderBy(j => j.Priority)
            .ThenBy(j => j.NextRunAt)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<bool> RemoveJobAsync(string jobId, CancellationToken ct = default)
    {
        var removed = _jobs.TryRemove(jobId, out _);
        _occurrences.TryRemove(jobId, out _);
        return Task.FromResult(removed);
    }

    public async Task<bool> TryLockJobAsync(string jobId, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (!_jobs.TryGetValue(jobId, out var job))
                return false;

            if (job.Status == JobStatus.Running || job.Status == JobStatus.Paused)
                return false;

            job.Status = JobStatus.Running;
            job.UpdatedAt = DateTimeOffset.UtcNow;
            return true;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task ReleaseJobAsync(string jobId, JobStatus newStatus, DateTimeOffset? nextRunAt, CancellationToken ct = default)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = newStatus;
            job.NextRunAt = nextRunAt;
            job.LastRunAt = DateTimeOffset.UtcNow;
            job.UpdatedAt = DateTimeOffset.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task AddOccurrenceAsync(JobOccurrenceRecord occurrence, CancellationToken ct = default)
    {
        var list = _occurrences.GetOrAdd(occurrence.JobId, _ => new List<JobOccurrenceRecord>());
        lock (list)
        {
            list.Add(occurrence);
        }
        return Task.CompletedTask;
    }

    public Task UpdateOccurrenceAsync(JobOccurrenceRecord occurrence, CancellationToken ct = default)
    {
        if (_occurrences.TryGetValue(occurrence.JobId, out var list))
        {
            lock (list)
            {
                var index = list.FindIndex(o => o.Id == occurrence.Id);
                if (index >= 0)
                    list[index] = occurrence;
            }
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<JobOccurrenceRecord>> GetOccurrencesAsync(string jobId, int limit = 20, CancellationToken ct = default)
    {
        if (_occurrences.TryGetValue(jobId, out var list))
        {
            lock (list)
            {
                IReadOnlyList<JobOccurrenceRecord> result = list
                    .OrderByDescending(o => o.StartedAt)
                    .Take(limit)
                    .ToList();
                return Task.FromResult(result);
            }
        }
        IReadOnlyList<JobOccurrenceRecord> empty = Array.Empty<JobOccurrenceRecord>();
        return Task.FromResult(empty);
    }
}
