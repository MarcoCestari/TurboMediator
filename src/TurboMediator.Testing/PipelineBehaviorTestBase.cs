using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing pipeline behaviors.
/// </summary>
/// <typeparam name="TBehavior">The behavior type.</typeparam>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public abstract class PipelineBehaviorTestBase<TBehavior, TMessage, TResponse>
    where TBehavior : class, IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
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
    /// Invokes the pipeline with a message and expected response.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="expectedResponse">The response that the next delegate should return.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The response from the behavior.</returns>
    protected ValueTask<TResponse> InvokePipeline(
        TMessage message,
        TResponse expectedResponse,
        CancellationToken cancellationToken = default)
    {
        return Behavior.Handle(
            message,
            (msg, ct) => new ValueTask<TResponse>(expectedResponse),
            cancellationToken);
    }

    /// <summary>
    /// Invokes the pipeline with a message and a custom next delegate.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="next">The next delegate in the pipeline.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The response from the behavior.</returns>
    protected ValueTask<TResponse> InvokePipeline(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken = default)
    {
        return Behavior.Handle(message, next, cancellationToken);
    }

    /// <summary>
    /// Invokes the pipeline expecting the next delegate to throw an exception.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="exception">The exception that the next delegate should throw.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>The response from the behavior (if it handles the exception).</returns>
    protected ValueTask<TResponse> InvokePipelineWithException(
        TMessage message,
        Exception exception,
        CancellationToken cancellationToken = default)
    {
        return Behavior.Handle(
            message,
            (msg, ct) => throw exception,
            cancellationToken);
    }

    /// <summary>
    /// Creates a tracking delegate that records when it's called.
    /// </summary>
    /// <param name="response">The response to return.</param>
    /// <returns>A tuple containing the delegate and a function to check if it was called.</returns>
    protected (MessageHandlerDelegate<TMessage, TResponse> Delegate, Func<bool> WasCalled, Func<int> CallCount) CreateTrackingDelegate(TResponse response)
    {
        var callCount = 0;
        MessageHandlerDelegate<TMessage, TResponse> del = (msg, ct) =>
        {
            Interlocked.Increment(ref callCount);
            return new ValueTask<TResponse>(response);
        };
        return (del, () => callCount > 0, () => callCount);
    }

    /// <summary>
    /// Resets the behavior instance. The next access to Behavior will create a new instance.
    /// </summary>
    protected void ResetBehavior() => _behavior = null;
}
