using TurboMediator;
using TurboMediator.Benchmarks.Shared;

namespace TurboMediator.Benchmarks.TurboHandlers;

// --- Command (void) ---
public record PingCommand : ICommand;

public class PingCommandHandler : ICommandHandler<PingCommand, Unit>
{
    public ValueTask<Unit> Handle(PingCommand command, CancellationToken cancellationToken)
        => Unit.ValueTask;
}

// --- Command with response ---
public record PingCommandWithResponse : ICommand<Pong>;

public class PingCommandWithResponseHandler : ICommandHandler<PingCommandWithResponse, Pong>
{
    public ValueTask<Pong> Handle(PingCommandWithResponse command, CancellationToken cancellationToken)
        => new(new Pong());
}

// --- Query ---
public record PingQuery : IQuery<Pong>;

public class PingQueryHandler : IQueryHandler<PingQuery, Pong>
{
    public ValueTask<Pong> Handle(PingQuery query, CancellationToken cancellationToken)
        => new(new Pong());
}

// --- Request ---
public record PingRequest : IRequest<Pong>;

public class PingRequestHandler : IRequestHandler<PingRequest, Pong>
{
    public ValueTask<Pong> Handle(PingRequest request, CancellationToken cancellationToken)
        => new(new Pong());
}

// --- Notification ---
public record PingNotification : INotification;

public class PingNotificationHandler : INotificationHandler<PingNotification>
{
    public ValueTask Handle(PingNotification notification, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

// --- Pipeline Behavior ---
public class TurboPipelineBehavior<TMessage, TResponse>
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
        => next();
}
