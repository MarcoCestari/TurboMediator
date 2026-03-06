using TurboMediator.Benchmarks.Shared;

namespace TurboMediator.Benchmarks.SourceGenHandlers;

// --- Command (void) ---
public record PingCommand : Mediator.ICommand;

public class PingCommandHandler : Mediator.ICommandHandler<PingCommand>
{
    public ValueTask<Mediator.Unit> Handle(PingCommand command, CancellationToken cancellationToken)
        => new(Mediator.Unit.Value);
}

// --- Command with response ---
public record PingCommandWithResponse : Mediator.ICommand<Pong>;

public class PingCommandWithResponseHandler : Mediator.ICommandHandler<PingCommandWithResponse, Pong>
{
    public ValueTask<Pong> Handle(PingCommandWithResponse command, CancellationToken cancellationToken)
        => new(new Pong());
}

// --- Query ---
public record PingQuery : Mediator.IQuery<Pong>;

public class PingQueryHandler : Mediator.IQueryHandler<PingQuery, Pong>
{
    public ValueTask<Pong> Handle(PingQuery query, CancellationToken cancellationToken)
        => new(new Pong());
}

// --- Notification ---
public record PingNotification : Mediator.INotification;

public class PingNotificationHandler : Mediator.INotificationHandler<PingNotification>
{
    public ValueTask Handle(PingNotification notification, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

// --- Pipeline Behavior ---
public class SourceGenPipelineBehavior<TMessage, TResponse>
    : Mediator.IPipelineBehavior<TMessage, TResponse>
    where TMessage : Mediator.IMessage
{
    public ValueTask<TResponse> Handle(
        TMessage message,
        CancellationToken cancellationToken,
        Mediator.MessageHandlerDelegate<TMessage, TResponse> next)
        => next(message, cancellationToken);
}
