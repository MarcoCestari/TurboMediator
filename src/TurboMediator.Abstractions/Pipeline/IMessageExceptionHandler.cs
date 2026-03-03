namespace TurboMediator;

/// <summary>
/// Defines a handler for exceptions thrown during message handling.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
/// <typeparam name="TException">The type of exception to handle.</typeparam>
public interface IMessageExceptionHandler<in TMessage, TResponse, in TException>
    where TMessage : IMessage
    where TException : Exception
{
    /// <summary>
    /// Handle an exception thrown during message processing.
    /// </summary>
    /// <param name="message">The message that was being processed.</param>
    /// <param name="exception">The exception that was thrown.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="ExceptionHandlingResult{TResponse}"/> indicating whether the exception
    /// was handled and optionally providing an alternative response.
    /// </returns>
    ValueTask<ExceptionHandlingResult<TResponse>> Handle(TMessage message, TException exception, CancellationToken cancellationToken);
}
