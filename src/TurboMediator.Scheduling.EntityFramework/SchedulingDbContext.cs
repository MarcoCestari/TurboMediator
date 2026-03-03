using System;
using Microsoft.EntityFrameworkCore;

namespace TurboMediator.Scheduling.EntityFramework;

/// <summary>
/// EF Core DbContext for recurring job scheduling persistence.
/// </summary>
public class SchedulingDbContext : DbContext
{
    /// <summary>Recurring job definitions.</summary>
    public DbSet<RecurringJobEntity> RecurringJobs { get; set; } = null!;

    /// <summary>Job execution occurrences.</summary>
    public DbSet<JobOccurrenceEntity> JobOccurrences { get; set; } = null!;

    /// <summary>
    /// Creates a new SchedulingDbContext.
    /// </summary>
    public SchedulingDbContext(DbContextOptions<SchedulingDbContext> options) : base(options) { }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RecurringJobEntity>(entity =>
        {
            entity.ToTable("SchedulingRecurringJobs");
            entity.HasKey(e => e.JobId);
            entity.Property(e => e.JobId).HasMaxLength(256);
            entity.Property(e => e.MessageTypeName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.CronExpression).HasMaxLength(128);
            entity.Property(e => e.MessagePayload).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Priority).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.RetryIntervalSecondsJson).HasColumnName("RetryIntervalSeconds").HasMaxLength(512);
            entity.Property(e => e.TimeZoneId).HasMaxLength(128);

            entity.HasIndex(e => new { e.Status, e.NextRunAt })
                  .HasDatabaseName("IX_SchedulingRecurringJobs_Status_NextRunAt");
        });

        modelBuilder.Entity<JobOccurrenceEntity>(entity =>
        {
            entity.ToTable("SchedulingJobOccurrences");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.JobId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.RunCondition).HasConversion<string>().HasMaxLength(32);
            entity.Property(e => e.Error).HasMaxLength(4000);

            entity.HasIndex(e => new { e.JobId, e.StartedAt })
                  .HasDatabaseName("IX_SchedulingJobOccurrences_JobId_StartedAt");
        });
    }
}

/// <summary>
/// EF Core entity for recurring job definitions.
/// </summary>
public class RecurringJobEntity
{
    public string JobId { get; set; } = string.Empty;
    public string MessageTypeName { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public long? IntervalTicks { get; set; }
    public DateTimeOffset? NextRunAt { get; set; }
    public DateTimeOffset? LastRunAt { get; set; }
    public JobStatus Status { get; set; }
    public string RetryIntervalSecondsJson { get; set; } = "[]";
    public bool SkipIfAlreadyRunning { get; set; }
    public JobPriority Priority { get; set; }
    public string MessagePayload { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? TimeZoneId { get; set; }
}

/// <summary>
/// EF Core entity for job execution occurrences.
/// </summary>
public class JobOccurrenceEntity
{
    public Guid Id { get; set; }
    public string JobId { get; set; } = string.Empty;
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public JobStatus Status { get; set; }
    public int RetryCount { get; set; }
    public string? Error { get; set; }
    public Guid? ParentOccurrenceId { get; set; }
    public RunCondition? RunCondition { get; set; }
}
