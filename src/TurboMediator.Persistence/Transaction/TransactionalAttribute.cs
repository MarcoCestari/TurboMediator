namespace TurboMediator.Persistence.Transaction;

/// <summary>
/// Attribute to mark a message for automatic transaction wrapping.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
public class TransactionalAttribute : Attribute
{
    /// <summary>
    /// Creates a new TransactionalAttribute with default settings.
    /// </summary>
    public TransactionalAttribute()
    {
    }

    /// <summary>
    /// Creates a new TransactionalAttribute with the specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    public TransactionalAttribute(System.Data.IsolationLevel isolationLevel)
    {
        IsolationLevel = isolationLevel;
    }

    /// <summary>
    /// Gets or sets the isolation level for the transaction.
    /// Default is ReadCommitted.
    /// </summary>
    public System.Data.IsolationLevel IsolationLevel { get; set; } = System.Data.IsolationLevel.ReadCommitted;

    /// <summary>
    /// Gets or sets the timeout in seconds for the transaction. Default is 30.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether to automatically save changes after the handler.
    /// Default is true.
    /// </summary>
    public bool AutoSaveChanges { get; set; } = true;
}
