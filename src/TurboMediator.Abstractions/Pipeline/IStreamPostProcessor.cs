namespace TurboMediator;

/// <summary>
/// Stream post-processor that wraps the stream after the handler.
/// Can be used for logging, metrics, or transforming stream items.
/// </summary>
/// <typeparam name="TMessage">The type of stream message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response items in the stream.</typeparam>
public interface IStreamPostProcessor<in TMessage, TResponse>
    where TMessage : IStreamMessage
{
    /// <summary>
    /// Wraps the stream and processes each item after yielding.
    /// </summary>
    /// <param name="message">The original stream message.</param>
    /// <param name="stream">The stream from the handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A wrapped async enumerable of response items.</returns>
    IAsyncEnumerable<TResponse> Process(
        TMessage message,
        IAsyncEnumerable<TResponse> stream,
        CancellationToken cancellationToken);
}
