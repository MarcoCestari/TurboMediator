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
/// Uses a generic <typeparamref name="TContext"/> so the application's own DbContext can be reused.
/// </summary>
/// <typeparam name="TContext">The DbContext type that contains the scheduling entity configurations.</typeparam>
public sealed class EfCoreJobStore<TContext> : IJobStore where TContext : DbContext
{
    private readonly TContext _context;
    private readonly EfCoreSchedulingStoreOptions _options;
    private static volatile bool _initialized;

    /// <summary>Creates a new EfCoreJobStore.</summary>
    public EfCoreJobStore(TContext context, EfCoreSchedulingStoreOptions options)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized || !_options.AutoMigrate) return;
        await _context.Database.EnsureCreatedAsync(ct);
        _initialized = true;
    }

    public async Task UpsertJobAsync(RecurringJobRecord job, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var set = _context.Set<RecurringJobEntity>();
        var entity = await set.FindAsync(new object[] { job.JobId }, ct);
        if (entity == null)
        {
            entity = new RecurringJobEntity();
            MapToEntity(job, entity);
            set.Add(entity);
        }
        else
        {
            MapToEntity(job, entity);
        }
        await _context.SaveChangesAsync(ct);
    }

    public async Task<RecurringJobRecord?> GetJobAsync(string jobId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var entity = await _context.Set<RecurringJobEntity>().AsNoTracking().FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        return entity == null ? null : MapToRecord(entity);
    }

    public async Task<IReadOnlyList<RecurringJobRecord>> GetAllJobsAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var entities = await _context.Set<RecurringJobEntity>().AsNoTracking().ToListAsync(ct);
        return entities.Select(MapToRecord).ToList();
    }

    public async Task<IReadOnlyList<RecurringJobRecord>> GetDueJobsAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var entities = await _context.Set<RecurringJobEntity>()
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
        await EnsureInitializedAsync(ct);
        var jobSet = _context.Set<RecurringJobEntity>();
        var occurrenceSet = _context.Set<JobOccurrenceEntity>();
        var entity = await jobSet.FindAsync(new object[] { jobId }, ct);
        if (entity == null)
            return false;

        // Remove all occurrences
        var occurrences = await occurrenceSet.Where(o => o.JobId == jobId).ToListAsync(ct);
        occurrenceSet.RemoveRange(occurrences);
        jobSet.Remove(entity);

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TryLockJobAsync(string jobId, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var entity = await _context.Set<RecurringJobEntity>().FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (entity == null || entity.Status == JobStatus.Running || entity.Status == JobStatus.Paused)
            return false;

        entity.Status = JobStatus.Running;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }

    public async Task ReleaseJobAsync(string jobId, JobStatus newStatus, DateTimeOffset? nextRunAt, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var entity = await _context.Set<RecurringJobEntity>().FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (entity != null)
        {
            entity.Status = newStatus;
            entity.NextRunAt = nextRunAt;
            entity.LastRunAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task AddOccurrenceAsync(JobOccurrenceRecord occurrence, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var entity = MapToOccurrenceEntity(occurrence);
        _context.Set<JobOccurrenceEntity>().Add(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateOccurrenceAsync(JobOccurrenceRecord occurrence, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var entity = await _context.Set<JobOccurrenceEntity>().FindAsync(new object[] { occurrence.Id }, ct);
        if (entity != null)
        {
            entity.Status = occurrence.Status;
            entity.CompletedAt = occurrence.CompletedAt;
            entity.RetryCount = occurrence.RetryCount;
            entity.Error = occurrence.Error;
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<JobOccurrenceRecord>> GetOccurrencesAsync(string jobId, int limit = 20, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);
        var entities = await _context.Set<JobOccurrenceEntity>()
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
