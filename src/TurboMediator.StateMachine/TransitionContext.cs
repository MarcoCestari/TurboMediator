namespace TurboMediator.StateMachine;

/// <summary>
/// Provides context during state transitions, including access to the mediator
/// for dispatching commands and notifications.
/// </summary>
public sealed class TransitionContext
{
    private readonly IMediator _mediator;
    private readonly Dictionary<string, string> _metadata;

    internal TransitionContext(IMediator mediator, IDictionary<string, string>? metadata = null)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _metadata = metadata != null ? new Dictionary<string, string>(metadata) : new();
    }

    /// <summary>
    /// Gets metadata associated with this transition.
    /// Can be used to pass additional context (e.g., reason for cancellation).
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata => _metadata;

    /// <summary>
    /// Sends a command via the mediator.
    /// </summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The command response.</returns>
    public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        => _mediator.Send(command, cancellationToken);

    /// <summary>
    /// Publishes a notification via the mediator.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification to publish.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => _mediator.Publish(notification, cancellationToken);
}
