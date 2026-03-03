using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Testing;

/// <summary>
/// Base class for testing message pre-processors.
/// </summary>
/// <typeparam name="TProcessor">The pre-processor type.</typeparam>
/// <typeparam name="TMessage">The message type.</typeparam>
public abstract class MessagePreProcessorTestBase<TProcessor, TMessage>
    where TProcessor : class, IMessagePreProcessor<TMessage>
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
    /// Processes a message using the pre-processor.
    /// </summary>
    /// <param name="message">The message to process.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    protected ValueTask Process(TMessage message, CancellationToken cancellationToken = default)
        => Processor.Process(message, cancellationToken);

    /// <summary>
    /// Resets the processor instance. The next access to Processor will create a new instance.
    /// </summary>
    protected void ResetProcessor() => _processor = null;
}
