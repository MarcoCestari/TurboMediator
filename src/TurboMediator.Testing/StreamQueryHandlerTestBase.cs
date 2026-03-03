using System.Collections.Generic;
using System.Threading;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing stream query handlers.
/// </summary>
/// <typeparam name="THandler">The handler type.</typeparam>
/// <typeparam name="TQuery">The stream query type.</typeparam>
/// <typeparam name="TResponse">The response item type.</typeparam>
public abstract class StreamQueryHandlerTestBase<THandler, TQuery, TResponse>
    where THandler : class, IStreamQueryHandler<TQuery, TResponse>
    where TQuery : IStreamQuery<TResponse>
{
    private THandler? _handler;

    /// <summary>
    /// Gets the handler instance. Creates it if not already created.
    /// </summary>
    protected THandler Handler => _handler ??= CreateHandler();

    /// <summary>
    /// Creates a new instance of the handler.
    /// Override this to provide the handler with its dependencies.
    /// </summary>
    protected abstract THandler CreateHandler();

    /// <summary>
    /// Handles a stream query using the handler.
    /// </summary>
    /// <param name="query">The stream query to handle.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An async enumerable of responses.</returns>
    protected IAsyncEnumerable<TResponse> Handle(TQuery query, CancellationToken cancellationToken = default)
        => Handler.Handle(query, cancellationToken);

    /// <summary>
    /// Resets the handler instance. The next access to Handler will create a new instance.
    /// </summary>
    protected void ResetHandler() => _handler = null;
}
