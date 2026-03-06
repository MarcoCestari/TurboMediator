// =============================================================
// MediatR (LuckyPennySoftware) - Messages & Handlers
// =============================================================

using MediatR;
using TurboMediator.Benchmarks.Shared;

namespace TurboMediator.Benchmarks.MediatRMessages;

// --- Command (void) ---
public record PingCommand : MediatR.IRequest;

public class PingCommandHandler : MediatR.IRequestHandler<PingCommand>
{
    public Task Handle(PingCommand request, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// --- Command with response ---
public record PingCommandWithResponse : MediatR.IRequest<Pong>;

public class PingCommandWithResponseHandler : MediatR.IRequestHandler<PingCommandWithResponse, Pong>
{
    public Task<Pong> Handle(PingCommandWithResponse request, CancellationToken cancellationToken)
        => Task.FromResult(new Pong());
}

// --- Notification ---
public record PingNotification : MediatR.INotification;

public class PingNotificationHandler : MediatR.INotificationHandler<PingNotification>
{
    public Task Handle(PingNotification notification, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

// --- Pipeline Behavior ---
public class MediatRPipelineBehavior<TRequest, TResponse>
    : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public Task<TResponse> Handle(
        TRequest request,
        MediatR.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
        => next();
}
