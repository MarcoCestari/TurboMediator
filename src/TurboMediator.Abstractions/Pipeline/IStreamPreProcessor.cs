namespace TurboMediator;

/// <summary>
/// Stream pre-processor that executes before the stream handler.
/// </summary>
/// <typeparam name="TMessage">The type of stream message being handled.</typeparam>
public interface IStreamPreProcessor<in TMessage>
    where TMessage : IStreamMessage
{
    /// <summary>
    /// Processes the message before the stream handler is invoked.
    /// </summary>
    /// <param name="message">The incoming stream message.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A ValueTask representing the async operation.</returns>
    ValueTask Process(TMessage message, CancellationToken cancellationToken);
}
