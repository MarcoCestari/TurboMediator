namespace TurboMediator.Saga.EntityFramework;

/// <summary>
/// Options for configuring the EF Core saga store.
/// </summary>
public class EfCoreSagaStoreOptions
{
    /// <summary>
    /// Gets or sets the table name for saga states.
    /// Default is "SagaStates".
    /// </summary>
    public string TableName { get; set; } = "SagaStates";

    /// <summary>
    /// Gets or sets the schema name for the saga states table.
    /// Default is null (uses default schema).
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets whether to enable optimistic concurrency using row versioning.
    /// Default is true.
    /// </summary>
    public bool EnableOptimisticConcurrency { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to automatically apply migrations for the saga table.
    /// Default is false.
    /// </summary>
    public bool AutoMigrate { get; set; }

    /// <summary>
    /// Gets or sets whether to use JSON column type for the Data field (SQL Server/PostgreSQL).
    /// Default is false (stores as NVARCHAR/TEXT).
    /// </summary>
    public bool UseJsonColumn { get; set; }
}
