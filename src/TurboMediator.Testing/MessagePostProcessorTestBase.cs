using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing message post-processors.
/// </summary>
/// <typeparam name="TProcessor">The post-processor type.</typeparam>
/// <typeparam name="TMessage">The message type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public abstract class MessagePostProcessorTestBase<TProcessor, TMessage, TResponse>
    where TProcessor : class, IMessagePostProcessor<TMessage, TResponse>
    where TMessage : IMessage
{
    private TProcessor? _processor;

    /// <summary>
    /// Gets the processor instance. Creates it if not already created.
    /// </summary>
    protected TProcessor Processor => _processor ??= CreateProcessor();

    /// <summary>
    /// Creates a new instance of the processor.
    /// Override this to provide the processor with its dependencies.
    /// </summary>
    protected abstract TProcessor CreateProcessor();

    /// <summary>
    /// Processes a message and response using the post-processor.
    /// </summary>
    /// <param name="message">The original message.</param>
    /// <param name="response">The response from the handler.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    protected ValueTask Process(TMessage message, TResponse response, CancellationToken cancellationToken = default)
        => Processor.Process(message, response, cancellationToken);

    /// <summary>
    /// Resets the processor instance. The next access to Processor will create a new instance.
    /// </summary>
    protected void ResetProcessor() => _processor = null;
}
