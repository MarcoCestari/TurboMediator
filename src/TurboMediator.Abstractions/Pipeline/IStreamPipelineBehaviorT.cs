namespace TurboMediator;

/// <summary>
/// Stream pipeline behavior to surround the inner stream handler.
/// Implementations add additional behavior and return the stream from the next delegate.
/// </summary>
/// <typeparam name="TMessage">The type of stream message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response items in the stream.</typeparam>
public interface IStreamPipelineBehavior<TMessage, TResponse>
    where TMessage : IStreamMessage
{
    /// <summary>
    /// Pipeline handler for streaming messages.
    /// </summary>
    /// <param name="message">The incoming stream message.</param>
    /// <param name="next">Delegate for the next action in the pipeline.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An async enumerable of response items.</returns>
    IAsyncEnumerable<TResponse> Handle(
        TMessage message,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}
