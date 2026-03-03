namespace TurboMediator.StateMachine.EntityFramework;

/// <summary>
/// Options for configuring the EF Core transition store.
/// </summary>
public class EfCoreTransitionStoreOptions
{
    /// <summary>
    /// Gets or sets the table name for transition records.
    /// Default is "StateTransitions".
    /// </summary>
    public string TableName { get; set; } = "StateTransitions";

    /// <summary>
    /// Gets or sets the schema name for the transition table.
    /// Default is null (uses default schema).
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets whether to automatically apply migrations for the transition table.
    /// Default is false.
    /// </summary>
    public bool AutoMigrate { get; set; }

    /// <summary>
    /// Gets or sets whether to use JSON column type for the Metadata field (SQL Server/PostgreSQL).
    /// Default is false (stores as NVARCHAR/TEXT).
    /// </summary>
    public bool UseJsonColumn { get; set; }
}
