using System;
using System.Data;

namespace TurboMediator.Persistence.Transaction;

/// <summary>
/// Options for configuring the TransactionBehavior.
/// </summary>
public class TransactionOptions
{
    /// <summary>
    /// Gets or sets the isolation level for the transaction.
    /// Default is ReadCommitted.
    /// </summary>
    public IsolationLevel IsolationLevel { get; set; } = IsolationLevel.ReadCommitted;

    /// <summary>
    /// Gets or sets the timeout for the transaction.
    /// Default is 30 seconds.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets whether to automatically call SaveChanges after the handler.
    /// Default is true.
    /// </summary>
    public bool AutoSaveChanges { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to use the execution strategy (retry on transient failures).
    /// Default is true.
    /// </summary>
    public bool UseExecutionStrategy { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to throw an exception if a transaction is already active.
    /// Default is false (will reuse existing transaction).
    /// </summary>
    public bool ThrowOnNestedTransaction { get; set; } = false;
}
