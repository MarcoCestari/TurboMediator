using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace TurboMediator.Scheduling.EntityFramework;

/// <summary>
/// Entity Framework Core implementation of <see cref="IJobStore"/>.
/// Provides persistent storage with optimistic concurrency for multi-instance deployments.
/// </summary>
public sealed class EfCoreJobStore : IJobStore
{
    private readonly SchedulingDbContext _db;

    /// <summary>Creates a new EfCoreJobStore.</summary>
    public EfCoreJobStore(SchedulingDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task UpsertJobAsync(RecurringJobRecord job, CancellationToken ct = default)
    {
        var entity = await _db.RecurringJobs.FindAsync(new object[] { job.JobId }, ct);
        if (entity == null)
        {
            entity = new RecurringJobEntity();
            MapToEntity(job, entity);
            _db.RecurringJobs.Add(entity);
        }
        else
        {
            MapToEntity(job, entity);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<RecurringJobRecord?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        var entity = await _db.RecurringJobs.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        return entity == null ? null : MapToRecord(entity);
    }

    public async Task<IReadOnlyList<RecurringJobRecord>> GetAllJobsAsync(CancellationToken ct = default)
    {
        var entities = await _db.RecurringJobs.AsNoTracking().ToListAsync(ct);
        return entities.Select(MapToRecord).ToList();
    }

    public async Task<IReadOnlyList<RecurringJobRecord>> GetDueJobsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var entities = await _db.RecurringJobs
            .AsNoTracking()
            .Where(j => j.Status != JobStatus.Paused
                        && j.Status != JobStatus.Running
                        && j.NextRunAt != null
                        && j.NextRunAt <= now)
            .OrderBy(j => j.Priority)
            .ThenBy(j => j.NextRunAt)
            .ToListAsync(ct);

        return entities.Select(MapToRecord).ToList();
    }

    public async Task<bool> RemoveJobAsync(string jobId, CancellationToken ct = default)
    {
        var entity = await _db.RecurringJobs.FindAsync(new object[] { jobId }, ct);
        if (entity == null)
            return false;

        // Remove all occurrences
        var occurrences = await _db.JobOccurrences.Where(o => o.JobId == jobId).ToListAsync(ct);
        _db.JobOccurrences.RemoveRange(occurrences);
        _db.RecurringJobs.Remove(entity);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TryLockJobAsync(string jobId, CancellationToken ct = default)
    {
        var entity = await _db.RecurringJobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (entity == null || entity.Status == JobStatus.Running || entity.Status == JobStatus.Paused)
            return false;

        entity.Status = JobStatus.Running;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task ReleaseJobAsync(string jobId, JobStatus newStatus, DateTimeOffset? nextRunAt, CancellationToken ct = default)
    {
        var entity = await _db.RecurringJobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (entity != null)
        {
            entity.Status = newStatus;
            entity.NextRunAt = nextRunAt;
            entity.LastRunAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task AddOccurrenceAsync(JobOccurrenceRecord occurrence, CancellationToken ct = default)
    {
        var entity = MapToOccurrenceEntity(occurrence);
        _db.JobOccurrences.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateOccurrenceAsync(JobOccurrenceRecord occurrence, CancellationToken ct = default)
    {
        var entity = await _db.JobOccurrences.FindAsync(new object[] { occurrence.Id }, ct);
        if (entity != null)
        {
            entity.Status = occurrence.Status;
            entity.CompletedAt = occurrence.CompletedAt;
            entity.RetryCount = occurrence.RetryCount;
            entity.Error = occurrence.Error;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<JobOccurrenceRecord>> GetOccurrencesAsync(string jobId, int limit = 20, CancellationToken ct = default)
    {
        var entities = await _db.JobOccurrences
            .AsNoTracking()
            .Where(o => o.JobId == jobId)
            .OrderByDescending(o => o.StartedAt)
            .Take(limit)
            .ToListAsync(ct);

        return entities.Select(MapToOccurrenceRecord).ToList();
    }

    #region Mapping

    private static void MapToEntity(RecurringJobRecord record, RecurringJobEntity entity)
    {
        entity.JobId = record.JobId;
        entity.MessageTypeName = record.MessageTypeName;
        entity.CronExpression = record.CronExpression;
        entity.IntervalTicks = record.Interval?.Ticks;
        entity.NextRunAt = record.NextRunAt;
        entity.LastRunAt = record.LastRunAt;
        entity.Status = record.Status;
        entity.RetryIntervalSecondsJson = JsonSerializer.Serialize(record.RetryIntervalSeconds);
        entity.SkipIfAlreadyRunning = record.SkipIfAlreadyRunning;
        entity.Priority = record.Priority;
        entity.MessagePayload = record.MessagePayload;
        entity.CreatedAt = record.CreatedAt;
        entity.UpdatedAt = record.UpdatedAt;
        entity.TimeZoneId = record.TimeZoneId;
    }

    private static RecurringJobRecord MapToRecord(RecurringJobEntity entity)
    {
        return new RecurringJobRecord
        {
            JobId = entity.JobId,
            MessageTypeName = entity.MessageTypeName,
            CronExpression = entity.CronExpression,
            Interval = entity.IntervalTicks.HasValue ? TimeSpan.FromTicks(entity.IntervalTicks.Value) : null,
            NextRunAt = entity.NextRunAt,
            LastRunAt = entity.LastRunAt,
            Status = entity.Status,
            RetryIntervalSeconds = JsonSerializer.Deserialize<int[]>(entity.RetryIntervalSecondsJson) ?? Array.Empty<int>(),
            SkipIfAlreadyRunning = entity.SkipIfAlreadyRunning,
            Priority = entity.Priority,
            MessagePayload = entity.MessagePayload,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt,
            TimeZoneId = entity.TimeZoneId
        };
    }

    private static JobOccurrenceEntity MapToOccurrenceEntity(JobOccurrenceRecord record)
    {
        return new JobOccurrenceEntity
        {
            Id = record.Id,
            JobId = record.JobId,
            StartedAt = record.StartedAt,
            CompletedAt = record.CompletedAt,
            Status = record.Status,
            RetryCount = record.RetryCount,
            Error = record.Error,
            ParentOccurrenceId = record.ParentOccurrenceId,
            RunCondition = record.RunCondition
        };
    }

    private static JobOccurrenceRecord MapToOccurrenceRecord(JobOccurrenceEntity entity)
    {
        return new JobOccurrenceRecord
        {
            Id = entity.Id,
            JobId = entity.JobId,
            StartedAt = entity.StartedAt,
            CompletedAt = entity.CompletedAt,
            Status = entity.Status,
            RetryCount = entity.RetryCount,
            Error = entity.Error,
            ParentOccurrenceId = entity.ParentOccurrenceId,
            RunCondition = entity.RunCondition
        };
    }

    #endregion
}
