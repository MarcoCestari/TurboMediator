using System.Collections.Generic;
using System.Threading;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing stream command handlers.
/// </summary>
/// <typeparam name="THandler">The handler type.</typeparam>
/// <typeparam name="TCommand">The stream command type.</typeparam>
/// <typeparam name="TResponse">The response item type.</typeparam>
public abstract class StreamCommandHandlerTestBase<THandler, TCommand, TResponse>
    where THandler : class, IStreamCommandHandler<TCommand, TResponse>
    where TCommand : IStreamCommand<TResponse>
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
    /// Handles a stream command using the handler.
    /// </summary>
    /// <param name="command">The stream command to handle.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>An async enumerable of responses.</returns>
    protected IAsyncEnumerable<TResponse> Handle(TCommand command, CancellationToken cancellationToken = default)
        => Handler.Handle(command, cancellationToken);

    /// <summary>
    /// Resets the handler instance. The next access to Handler will create a new instance.
    /// </summary>
    protected void ResetHandler() => _handler = null;
}
