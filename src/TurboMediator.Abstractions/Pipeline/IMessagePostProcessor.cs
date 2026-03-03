namespace TurboMediator;

/// <summary>
/// Defines a post-processor for a message.
/// Executes after the handler has completed successfully.
/// </summary>
/// <typeparam name="TMessage">The type of message being processed.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public interface IMessagePostProcessor<in TMessage, in TResponse>
    where TMessage : IMessage
{
    /// <summary>
    /// Process the message after the handler has completed.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    /// <param name="response">The response from the handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask Process(TMessage message, TResponse response, CancellationToken cancellationToken);
}
