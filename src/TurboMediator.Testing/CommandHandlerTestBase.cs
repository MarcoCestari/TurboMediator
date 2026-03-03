using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing command handlers.
/// </summary>
/// <typeparam name="THandler">The handler type.</typeparam>
/// <typeparam name="TCommand">The command type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public abstract class CommandHandlerTestBase<THandler, TCommand, TResponse>
    where THandler : class, ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
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
    /// Handles a command using the handler.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The response from the handler.</returns>
    protected ValueTask<TResponse> Handle(TCommand command, CancellationToken cancellationToken = default)
        => Handler.Handle(command, cancellationToken);

    /// <summary>
    /// Resets the handler instance. The next access to Handler will create a new instance.
    /// </summary>
    protected void ResetHandler() => _handler = null;
}
