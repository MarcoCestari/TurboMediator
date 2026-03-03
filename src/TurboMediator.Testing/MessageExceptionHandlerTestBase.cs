using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing message exception handlers.
/// </summary>
/// <typeparam name="THandler">The exception handler type.</typeparam>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <typeparam name="TException">The exception type.</typeparam>
public abstract class MessageExceptionHandlerTestBase<THandler, TMessage, TResponse, TException>
    where THandler : class, IMessageExceptionHandler<TMessage, TResponse, TException>
    where TMessage : IMessage
    where TException : Exception
{
    private THandler? _handler;

    /// <summary>
    /// Gets the handler instance. Creates it if not already created.
    /// </summary>
    protected THandler Handler => _handler ??= CreateHandler();

    /// <summary>
    /// Creates a new instance of the exception handler.
    /// Override this to provide the handler with its dependencies.
    /// </summary>
    protected abstract THandler CreateHandler();

    /// <summary>
    /// Handles an exception using the exception handler.
    /// </summary>
    /// <param name="message">The message that caused the exception.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The exception handling result.</returns>
    protected ValueTask<ExceptionHandlingResult<TResponse>> Handle(
        TMessage message,
        TException exception,
        CancellationToken cancellationToken = default)
        => Handler.Handle(message, exception, cancellationToken);

    /// <summary>
    /// Resets the handler instance. The next access to Handler will create a new instance.
    /// </summary>
    protected void ResetHandler() => _handler = null;
}
