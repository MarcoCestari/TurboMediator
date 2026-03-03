using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Observability.Telemetry;

/// <summary>
/// Pipeline behavior that adds OpenTelemetry tracing and metrics to message handling.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class TelemetryBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly TelemetryOptions _options;

    /// <summary>
    /// Creates a new TelemetryBehavior with default options.
    /// </summary>
    public TelemetryBehavior() : this(new TelemetryOptions()) { }

    /// <summary>
    /// Creates a new TelemetryBehavior with the specified options.
    /// </summary>
    /// <param name="options">The telemetry options.</param>
    public TelemetryBehavior(TelemetryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage).Name;

        // Record request metric
        if (_options.RecordMetrics)
        {
            TurboMediatorTelemetry.RecordRequest(messageType);
        }

#if NET6_0_OR_GREATER
        // Start activity/span
        using var activity = _options.RecordTraces
            ? TurboMediatorTelemetry.StartActivity(messageType)
            : null;

        if (activity != null)
        {
            activity.SetTag("messaging.system", "turbomediator");
            activity.SetTag("messaging.operation", "process");
            activity.SetTag("turbomediator.message.type", messageType);
            activity.SetTag("turbomediator.response.type", typeof(TResponse).Name);

            // Add custom tags from message if it implements ITelemetryEnriched
            if (message is ITelemetryEnriched enriched)
            {
                foreach (var tag in enriched.GetTelemetryTags())
                {
                    activity.SetTag(tag.Key, tag.Value);
                }
            }
        }
#endif

        var stopwatch = Stopwatch.StartNew();
        var success = false;

        try
        {
            var response = await next();
            success = true;

#if NET6_0_OR_GREATER
            activity?.SetStatus(ActivityStatusCode.Ok);
#endif

            if (_options.RecordMetrics)
            {
                TurboMediatorTelemetry.RecordSuccess(messageType);
            }

            return response;
        }
        catch (Exception ex)
        {
#if NET6_0_OR_GREATER
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("exception.type", ex.GetType().FullName);
            activity?.SetTag("exception.message", ex.Message);

            if (_options.RecordExceptionStackTrace)
            {
                activity?.SetTag("exception.stacktrace", ex.StackTrace);
            }
#endif

            if (_options.RecordMetrics)
            {
                TurboMediatorTelemetry.RecordFailure(messageType, ex.GetType().Name);
            }

            throw;
        }
        finally
        {
            stopwatch.Stop();

            if (_options.RecordMetrics)
            {
                TurboMediatorTelemetry.RecordDuration(messageType, stopwatch.Elapsed.TotalMilliseconds, success);
            }
        }
    }
}
