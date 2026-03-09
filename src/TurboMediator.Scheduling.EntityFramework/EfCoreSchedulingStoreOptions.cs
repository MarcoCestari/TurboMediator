namespace TurboMediator.Scheduling.EntityFramework;

/// <summary>
/// Options for configuring the EF Core scheduling store.
/// </summary>
public class EfCoreSchedulingStoreOptions
{
    /// <summary>
    /// Gets or sets the table name for recurring jobs.
    /// Default is "SchedulingRecurringJobs".
    /// </summary>
    public string RecurringJobsTableName { get; set; } = "SchedulingRecurringJobs";

    /// <summary>
    /// Gets or sets the table name for job occurrences.
    /// Default is "SchedulingJobOccurrences".
    /// </summary>
    public string JobOccurrencesTableName { get; set; } = "SchedulingJobOccurrences";

    /// <summary>
    /// Gets or sets the schema name for the scheduling tables.
    /// Default is null (uses default schema).
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically ensure the database is created.
    /// Default is false.
    /// </summary>
    public bool AutoMigrate { get; set; }
}
