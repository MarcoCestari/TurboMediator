using System.Diagnostics;
using TurboMediator;

namespace TurboMediator.Sample;

// ============ Pipeline Behaviors ============

/// <summary>
/// Logging behavior that logs before and after each request.
/// </summary>
public class LoggingBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var messageName = typeof(TMessage).Name;
        Console.WriteLine($"   📥 [LoggingBehavior] Handling {messageName}...");

        var stopwatch = Stopwatch.StartNew();
        var response = await next(message, cancellationToken);
        stopwatch.Stop();

        Console.WriteLine($"   📤 [LoggingBehavior] Handled {messageName} in {stopwatch.ElapsedMilliseconds}ms");

        return response;
    }
}

/// <summary>
/// Performance behavior that tracks slow requests.
/// </summary>
public class PerformanceBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private const int SlowRequestThresholdMs = 100;

    public async ValueTask<TResponse> Handle(TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next(message, cancellationToken);
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            Console.WriteLine($"   ⚠️ [PerformanceBehavior] SLOW REQUEST: {typeof(TMessage).Name} took {stopwatch.ElapsedMilliseconds}ms");
        }

        return response;
    }
}

// ============ Pre/Post Processors ============

/// <summary>
/// Pre-processor that validates requests before handling.
/// </summary>
public class ValidationPreProcessor<TMessage> : IMessagePreProcessor<TMessage>
    where TMessage : IMessage
{
    public ValueTask Process(TMessage message, CancellationToken cancellationToken)
    {
        Console.WriteLine($"   ✔️ [ValidationPreProcessor] Validating {typeof(TMessage).Name}...");
        // In a real app, you would validate the message here
        return default;
    }
}

/// <summary>
/// Post-processor that logs successful completions.
/// </summary>
public class AuditPostProcessor<TMessage, TResponse> : IMessagePostProcessor<TMessage, TResponse>
    where TMessage : IMessage
{
    public ValueTask Process(TMessage message, TResponse response, CancellationToken cancellationToken)
    {
        Console.WriteLine($"   📝 [AuditPostProcessor] Auditing {typeof(TMessage).Name} completed successfully");
        return default;
    }
}

// ============ Exception Handlers ============

/// <summary>
/// Global exception handler that catches and handles exceptions.
/// </summary>
public class GlobalExceptionHandler<TMessage, TResponse> : IMessageExceptionHandler<TMessage, TResponse, Exception>
    where TMessage : IMessage
{
    public ValueTask<ExceptionHandlingResult<TResponse>> Handle(TMessage message, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"   ❌ [GlobalExceptionHandler] Caught exception in {typeof(TMessage).Name}: {exception.Message}");

        // In this example, we let the exception propagate
        // In a real app, you might return a default response or log to external service
        return new ValueTask<ExceptionHandlingResult<TResponse>>(
            ExceptionHandlingResult<TResponse>.NotHandled());
    }
}
