namespace TurboMediator.Resilience.Timeout;

/// <summary>
/// Pipeline behavior that enforces timeouts on message handlers.
/// </summary>
/// <typeparam name="TMessage">The type of message.</typeparam>
/// <typeparam name="TResponse">The type of response.</typeparam>
public class TimeoutBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly TimeSpan _defaultTimeout;

    /// <summary>
    /// Creates a new TimeoutBehavior with default timeout of 30 seconds.
    /// </summary>
    public TimeoutBehavior() : this(TimeSpan.FromSeconds(30)) { }

    /// <summary>
    /// Creates a new TimeoutBehavior with the specified default timeout.
    /// </summary>
    /// <param name="defaultTimeout">The default timeout to use when no attribute is specified.</param>
    public TimeoutBehavior(TimeSpan defaultTimeout)
    {
        _defaultTimeout = defaultTimeout;
    }

    /// <summary>
    /// Creates a new TimeoutBehavior from <see cref="TimeoutOptions"/>.
    /// </summary>
    /// <param name="options">The timeout options.</param>
    public TimeoutBehavior(TimeoutOptions options)
        : this(options?.DefaultTimeout ?? TimeSpan.FromSeconds(30))
    {
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var timeout = GetTimeout(message);

        // Use Task.WhenAny to race between the actual operation and a timeout
        var task = next(message, cancellationToken).AsTask();
        var delayTask = Task.Delay(timeout, cancellationToken);

        var completedTask = await Task.WhenAny(task, delayTask);

        if (completedTask == delayTask)
        {
            // Timeout occurred
            throw new TimeoutException($"The operation timed out after {timeout.TotalMilliseconds}ms for message type {typeof(TMessage).Name}.");
        }

        // The actual task completed - await it to propagate any exceptions
        return await task;
    }

    private TimeSpan GetTimeout(TMessage message)
    {
        var timeoutAttr = message.GetType().GetCustomAttributes(typeof(TimeoutAttribute), false)
            .OfType<TimeoutAttribute>()
            .FirstOrDefault();

        return timeoutAttr?.Timeout ?? _defaultTimeout;
    }
}
