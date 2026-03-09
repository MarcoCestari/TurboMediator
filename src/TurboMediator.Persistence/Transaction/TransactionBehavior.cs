using System.Reflection;

namespace TurboMediator.Persistence.Transaction;

/// <summary>
/// Pipeline behavior that wraps handler execution in a database transaction.
/// Automatically commits on success and rolls back on failure.
/// Works with any ITransactionManager implementation (EF Core, Dapper, ADO.NET, etc.).
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class TransactionBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ITransactionManager _transactionManager;
    private readonly TransactionOptions _options;

    /// <summary>
    /// Creates a new TransactionBehavior with the specified transaction manager and options.
    /// </summary>
    /// <param name="transactionManager">The transaction manager to use.</param>
    /// <param name="options">The transaction options.</param>
    public TransactionBehavior(ITransactionManager transactionManager, TransactionOptions? options = null)
    {
        _transactionManager = transactionManager ?? throw new ArgumentNullException(nameof(transactionManager));
        _options = options ?? new TransactionOptions();
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Check for attribute on message type
        var attribute = typeof(TMessage).GetCustomAttribute<TransactionalAttribute>();

        // If no attribute and not explicitly configured, just pass through
        if (attribute == null && _options == null)
        {
            return await next(message, cancellationToken);
        }

        // Merge attribute settings with options
        var isolationLevel = attribute?.IsolationLevel ?? _options.IsolationLevel;
        var autoSaveChanges = attribute?.AutoSaveChanges ?? _options.AutoSaveChanges;
        var useStrategy = _options.UseExecutionStrategy;

        // Extract timeout from attribute or options
        var timeoutSeconds = attribute?.TimeoutSeconds ?? (int)_options.Timeout.TotalSeconds;
        var timeout = TimeSpan.FromSeconds(timeoutSeconds);

        // Create a linked CTS with the configured timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);
        var effectiveToken = timeoutCts.Token;

        // Check if transaction is already active
        if (_transactionManager.HasActiveTransaction)
        {
            if (_options.ThrowOnNestedTransaction)
            {
                throw new InvalidOperationException(
                    "A transaction is already active. Nested transactions are not supported.");
            }

            // Reuse existing transaction
            return await ExecuteWithinTransaction(message, next, autoSaveChanges, effectiveToken);
        }

        // Execute with or without execution strategy
        if (useStrategy)
        {
            var result = await _transactionManager.ExecuteWithStrategyAsync(
                async ct =>
                {
                    return await ExecuteInNewTransaction(message, next, isolationLevel, autoSaveChanges, ct);
                },
                effectiveToken);
            return result;
        }

        return await ExecuteInNewTransaction(message, next, isolationLevel, autoSaveChanges, effectiveToken);
    }

    private async ValueTask<TResponse> ExecuteInNewTransaction(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        System.Data.IsolationLevel isolationLevel,
        bool autoSaveChanges,
        CancellationToken cancellationToken)
    {
        var transaction = await _transactionManager.BeginTransactionAsync(isolationLevel, cancellationToken);

        try
        {
            var response = await ExecuteWithinTransaction(message, next, autoSaveChanges, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await transaction.DisposeAsync();
        }
    }

    private async ValueTask<TResponse> ExecuteWithinTransaction(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        bool autoSaveChanges,
        CancellationToken cancellationToken)
    {
        var response = await next(message, cancellationToken);

        if (autoSaveChanges)
        {
            await _transactionManager.SaveChangesAsync(cancellationToken);
        }

        return response;
    }
}
