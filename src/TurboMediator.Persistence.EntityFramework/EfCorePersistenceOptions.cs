namespace TurboMediator.Persistence.EntityFramework;

/// <summary>
/// Options for configuring the EF Core persistence stores.
/// </summary>
public class EfCorePersistenceOptions
{
    /// <summary>
    /// Gets or sets the schema name for all persistence tables.
    /// Default is null (uses default schema).
    /// </summary>
    public string? SchemaName { get; set; }

    /// <summary>
    /// Gets or sets the table name for inbox messages.
    /// Default is "InboxMessages".
    /// </summary>
    public string InboxTableName { get; set; } = "InboxMessages";

    /// <summary>
    /// Gets or sets the table name for outbox messages.
    /// Default is "OutboxMessages".
    /// </summary>
    public string OutboxTableName { get; set; } = "OutboxMessages";

    /// <summary>
    /// Gets or sets the table name for audit entries.
    /// Default is "AuditEntries".
    /// </summary>
    public string AuditTableName { get; set; } = "AuditEntries";

    /// <summary>
    /// Gets or sets whether to automatically ensure the database is created.
    /// Default is false.
    /// </summary>
    public bool AutoMigrate { get; set; }

    /// <summary>
    /// Gets or sets whether to use JSON column type for payload fields (SQL Server/PostgreSQL).
    /// Default is false (stores as NVARCHAR/TEXT).
    /// </summary>
    public bool UseJsonColumn { get; set; }
}
