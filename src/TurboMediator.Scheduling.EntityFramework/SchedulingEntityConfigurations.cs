using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace TurboMediator.Scheduling.EntityFramework;

/// <summary>
/// Entity configuration for <see cref="RecurringJobEntity"/>.
/// </summary>
public class RecurringJobEntityConfiguration : IEntityTypeConfiguration<RecurringJobEntity>
{
    private readonly EfCoreSchedulingStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringJobEntityConfiguration"/> class.
    /// </summary>
    /// <param name="options">The scheduling store options.</param>
    public RecurringJobEntityConfiguration(EfCoreSchedulingStoreOptions? options = null)
    {
        _options = options ?? new EfCoreSchedulingStoreOptions();
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RecurringJobEntity> builder)
    {
        if (!string.IsNullOrEmpty(_options.SchemaName))
        {
            builder.ToTable(_options.RecurringJobsTableName, _options.SchemaName);
        }
        else
        {
            builder.ToTable(_options.RecurringJobsTableName);
        }

        builder.HasKey(e => e.JobId);
        builder.Property(e => e.JobId).HasMaxLength(256);
        builder.Property(e => e.MessageTypeName).HasMaxLength(512).IsRequired();
        builder.Property(e => e.CronExpression).HasMaxLength(128);
        builder.Property(e => e.MessagePayload).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Priority).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RetryIntervalSecondsJson).HasColumnName("RetryIntervalSeconds").HasMaxLength(512);
        builder.Property(e => e.TimeZoneId).HasMaxLength(128);

        builder.HasIndex(e => new { e.Status, e.NextRunAt })
              .HasDatabaseName("IX_SchedulingRecurringJobs_Status_NextRunAt");
    }
}

/// <summary>
/// Entity configuration for <see cref="JobOccurrenceEntity"/>.
/// </summary>
public class JobOccurrenceEntityConfiguration : IEntityTypeConfiguration<JobOccurrenceEntity>
{
    private readonly EfCoreSchedulingStoreOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobOccurrenceEntityConfiguration"/> class.
    /// </summary>
    /// <param name="options">The scheduling store options.</param>
    public JobOccurrenceEntityConfiguration(EfCoreSchedulingStoreOptions? options = null)
    {
        _options = options ?? new EfCoreSchedulingStoreOptions();
    }

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<JobOccurrenceEntity> builder)
    {
        if (!string.IsNullOrEmpty(_options.SchemaName))
        {
            builder.ToTable(_options.JobOccurrencesTableName, _options.SchemaName);
        }
        else
        {
            builder.ToTable(_options.JobOccurrencesTableName);
        }

        builder.HasKey(e => e.Id);
        builder.Property(e => e.JobId).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.RunCondition).HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.Error).HasMaxLength(4000);

        builder.HasIndex(e => new { e.JobId, e.StartedAt })
              .HasDatabaseName("IX_SchedulingJobOccurrences_JobId_StartedAt");
    }
}
