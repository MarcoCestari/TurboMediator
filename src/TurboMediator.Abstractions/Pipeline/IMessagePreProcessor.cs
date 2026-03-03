namespace TurboMediator;

/// <summary>
/// Defines a pre-processor for a message.
/// Executes before the handler is invoked.
/// </summary>
/// <typeparam name="TMessage">The type of message being processed.</typeparam>
public interface IMessagePreProcessor<in TMessage>
    where TMessage : IMessage
{
    /// <summary>
    /// Process the message before the handler is invoked.
    /// </summary>
    /// <param name="message">The incoming message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask Process(TMessage message, CancellationToken cancellationToken);
}
