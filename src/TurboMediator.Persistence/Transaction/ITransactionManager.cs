using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Persistence.Transaction;

/// <summary>
/// Abstraction for managing database transactions, independent of any specific ORM or data access technology.
/// Implement this interface to provide transaction support for EF Core, Dapper, ADO.NET, or any other provider.
/// </summary>
public interface ITransactionManager
{
    /// <summary>
    /// Gets whether a transaction is currently active.
    /// </summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Begins a new transaction with the specified isolation level.
    /// </summary>
    /// <param name="isolationLevel">The isolation level for the transaction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A transaction scope that can be committed or rolled back.</returns>
    ValueTask<ITransactionScope> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the given operation with a retry strategy for transient failures.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="operation">The operation to execute.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    ValueTask<TResult> ExecuteWithStrategyAsync<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves any pending changes (e.g., SaveChangesAsync in EF Core, or no-op for Dapper).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a transaction scope that can be committed or rolled back.
/// </summary>
public interface ITransactionScope : IAsyncDisposable
{
    /// <summary>
    /// Commits the transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask RollbackAsync(CancellationToken cancellationToken = default);
}
