using System.Collections.Generic;
using System.Diagnostics;

namespace TurboMediator.Observability.Telemetry;

/// <summary>
/// Provides OpenTelemetry instrumentation for TurboMediator.
/// </summary>
public static class TurboMediatorTelemetry
{
    /// <summary>
    /// The name of the activity source for TurboMediator.
    /// </summary>
    public const string ActivitySourceName = "TurboMediator";

#if NET6_0_OR_GREATER
    /// <summary>
    /// The name of the meter for TurboMediator metrics.
    /// </summary>
    public const string MeterName = "TurboMediator";

    /// <summary>
    /// The activity source for creating spans.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    /// <summary>
    /// The meter for recording metrics.
    /// </summary>
    public static readonly System.Diagnostics.Metrics.Meter Meter = new(MeterName, "1.0.0");

    // Metrics
    private static readonly System.Diagnostics.Metrics.Counter<long> _requestCounter = Meter.CreateCounter<long>(
        "turbomediator.requests.total",
        "requests",
        "Total number of requests processed");

    private static readonly System.Diagnostics.Metrics.Counter<long> _requestSuccessCounter = Meter.CreateCounter<long>(
        "turbomediator.requests.success",
        "requests",
        "Total number of successful requests");

    private static readonly System.Diagnostics.Metrics.Counter<long> _requestFailureCounter = Meter.CreateCounter<long>(
        "turbomediator.requests.failure",
        "requests",
        "Total number of failed requests");

    private static readonly System.Diagnostics.Metrics.Histogram<double> _requestDuration = Meter.CreateHistogram<double>(
        "turbomediator.requests.duration",
        "ms",
        "Request processing duration in milliseconds");

    private static readonly System.Diagnostics.Metrics.Counter<long> _notificationCounter = Meter.CreateCounter<long>(
        "turbomediator.notifications.total",
        "notifications",
        "Total number of notifications published");
#else
    // ActivitySource is not available on netstandard2.0, telemetry features are limited
#endif

    /// <summary>
    /// Records a request being processed.
    /// </summary>
    public static void RecordRequest(string messageType)
    {
#if NET6_0_OR_GREATER
        _requestCounter.Add(1, new KeyValuePair<string, object?>("message.type", messageType));
#endif
    }

    /// <summary>
    /// Records a successful request.
    /// </summary>
    public static void RecordSuccess(string messageType)
    {
#if NET6_0_OR_GREATER
        _requestSuccessCounter.Add(1, new KeyValuePair<string, object?>("message.type", messageType));
#endif
    }

    /// <summary>
    /// Records a failed request.
    /// </summary>
    public static void RecordFailure(string messageType, string? exceptionType = null)
    {
#if NET6_0_OR_GREATER
        var tags = new List<KeyValuePair<string, object?>>
        {
            new("message.type", messageType)
        };

        if (exceptionType != null)
        {
            tags.Add(new("exception.type", exceptionType));
        }

        _requestFailureCounter.Add(1, tags.ToArray());
#endif
    }

    /// <summary>
    /// Records the duration of a request.
    /// </summary>
    public static void RecordDuration(string messageType, double durationMs, bool success)
    {
#if NET6_0_OR_GREATER
        _requestDuration.Record(durationMs,
            new KeyValuePair<string, object?>("message.type", messageType),
            new KeyValuePair<string, object?>("status", success ? "success" : "failure"));
#endif
    }

    /// <summary>
    /// Records a notification being published.
    /// </summary>
    public static void RecordNotification(string notificationType, int handlerCount)
    {
#if NET6_0_OR_GREATER
        _notificationCounter.Add(1,
            new KeyValuePair<string, object?>("notification.type", notificationType),
            new KeyValuePair<string, object?>("handler.count", handlerCount));
#endif
    }

#if NET6_0_OR_GREATER
    /// <summary>
    /// Starts an activity for the given operation.
    /// </summary>
    public static Activity? StartActivity(string operationName, ActivityKind kind = ActivityKind.Internal)
    {
        return ActivitySource.StartActivity(operationName, kind);
    }
#endif
}
