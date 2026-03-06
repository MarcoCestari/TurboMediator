using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TurboMediator;

namespace Sample.RealWorld.Application;

/// <summary>
/// Custom pipeline behavior that logs execution time and warns on slow operations.
/// Demonstrates how to write cross-cutting concerns with IPipelineBehavior.
/// </summary>
public class PerformanceMonitoringBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ILogger<PerformanceMonitoringBehavior<TMessage, TResponse>> _logger;

    public PerformanceMonitoringBehavior(ILogger<PerformanceMonitoringBehavior<TMessage, TResponse>> logger)
        => _logger = logger;

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken ct)
    {
        var messageName = typeof(TMessage).Name;
        var sw = Stopwatch.StartNew();

        _logger.LogInformation("[Pipeline] {MessageType} started", messageName);

        var response = await next(message, ct);
        sw.Stop();

        if (sw.ElapsedMilliseconds > 500)
            _logger.LogWarning("[Pipeline] {MessageType} completed in {ElapsedMs}ms (SLOW)",
                messageName, sw.ElapsedMilliseconds);
        else
            _logger.LogInformation("[Pipeline] {MessageType} completed in {ElapsedMs}ms",
                messageName, sw.ElapsedMilliseconds);

        return response;
    }
}
