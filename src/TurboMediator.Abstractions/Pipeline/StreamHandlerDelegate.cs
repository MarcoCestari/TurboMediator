namespace TurboMediator;

/// <summary>
/// Delegate representing the next stream handler in the pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of items in the stream.</typeparam>
/// <returns>An async enumerable of response items.</returns>
public delegate IAsyncEnumerable<TResponse> StreamHandlerDelegate<TResponse>();
