using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Transaction;

namespace TurboMediator.Persistence.EntityFramework.Transaction;

/// <summary>
/// EF Core implementation of ITransactionManager.
/// Wraps DbContext to provide transaction management.
/// </summary>
/// <typeparam name="TContext">The DbContext type to manage transactions for.</typeparam>
public class EfCoreTransactionManager<TContext> : ITransactionManager where TContext : DbContext
{
    private readonly TContext _context;

    /// <summary>
    /// Creates a new EfCoreTransactionManager.
    /// </summary>
    /// <param name="context">The DbContext to manage transactions for.</param>
    public EfCoreTransactionManager(TContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public bool HasActiveTransaction => _context.Database.CurrentTransaction != null;

    /// <inheritdoc />
    public async ValueTask<ITransactionScope> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _context.Database.BeginTransactionAsync(isolationLevel, cancellationToken);
        return new EfCoreTransactionScope(transaction);
    }

    /// <inheritdoc />
    public async ValueTask<TResult> ExecuteWithStrategyAsync<TResult>(
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async ct =>
            {
                var result = await operation(ct);
                return result;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}

/// <summary>
/// EF Core implementation of ITransactionScope.
/// Wraps an EF Core IDbContextTransaction.
/// </summary>
internal class EfCoreTransactionScope : ITransactionScope
{
    private readonly Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction;

    public EfCoreTransactionScope(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        _transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
    }

    /// <inheritdoc />
    public async ValueTask CommitAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.CommitAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask RollbackAsync(CancellationToken cancellationToken = default)
    {
        await _transaction.RollbackAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _transaction.DisposeAsync();
    }
}
