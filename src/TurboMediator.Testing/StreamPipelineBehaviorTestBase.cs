using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing stream pipeline behaviors.
/// </summary>
/// <typeparam name="TBehavior">The behavior type.</typeparam>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResponse">The response item type.</typeparam>
public abstract class StreamPipelineBehaviorTestBase<TBehavior, TMessage, TResponse>
    where TBehavior : class, IStreamPipelineBehavior<TMessage, TResponse>
    where TMessage : IStreamMessage
{
    private TBehavior? _behavior;

    /// <summary>
    /// Gets the behavior instance. Creates it if not already created.
    /// </summary>
    protected TBehavior Behavior => _behavior ??= CreateBehavior();

    /// <summary>
    /// Creates a new instance of the behavior.
    /// Override this to provide the behavior with its dependencies.
    /// </summary>
    protected abstract TBehavior CreateBehavior();

    /// <summary>
    /// Invokes the stream pipeline with a message and expected items.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="expectedItems">The items that the next delegate should yield.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The async enumerable from the behavior.</returns>
    protected IAsyncEnumerable<TResponse> InvokePipeline(
        TMessage message,
        TResponse[] expectedItems,
        CancellationToken cancellationToken = default)
    {
        return Behavior.Handle(
            message,
            () => ToAsyncEnumerable(expectedItems),
            cancellationToken);
    }

    /// <summary>
    /// Invokes the stream pipeline with a message and a custom next delegate.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The async enumerable from the behavior.</returns>
    protected IAsyncEnumerable<TResponse> InvokePipeline(
        TMessage message,
        StreamHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken = default)
    {
        return Behavior.Handle(message, next, cancellationToken);
    }

    /// <summary>
    /// Resets the behavior instance. The next access to Behavior will create a new instance.
    /// </summary>
    protected void ResetBehavior() => _behavior = null;

    private static async IAsyncEnumerable<TResponse> ToAsyncEnumerable(TResponse[] items)
    {
        foreach (var item in items)
        {
            yield return item;
        }
        await Task.CompletedTask;
    }
}
