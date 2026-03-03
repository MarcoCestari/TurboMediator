using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Observability.Metrics;

/// <summary>
/// Pipeline behavior that collects metrics for message handling.
/// </summary>
/// <typeparam name="TMessage">The type of message being handled.</typeparam>
/// <typeparam name="TResponse">The type of response from the handler.</typeparam>
public class MetricsBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>, IDisposable
    where TMessage : IMessage
{
    private readonly MetricsOptions _options;
    private readonly Meter _meter;
    private readonly Histogram<double>? _latencyHistogram;
    private readonly Counter<long>? _throughputCounter;
    private readonly Counter<long>? _errorCounter;
    private readonly UpDownCounter<int>? _inFlightGauge;
    private readonly string _messageType;
    private readonly string _messageCategory;
    private bool _disposed;

    /// <summary>
    /// Creates a new MetricsBehavior.
    /// </summary>
    /// <param name="options">The metrics options.</param>
    public MetricsBehavior(MetricsOptions? options = null)
    {
        _options = options ?? new MetricsOptions();
        _messageType = typeof(TMessage).Name;
        _messageCategory = GetMessageCategory();

        _meter = new Meter(_options.MeterName, _options.MeterVersion);

        if (_options.EnableLatencyHistogram)
        {
            _latencyHistogram = _meter.CreateHistogram<double>(
                "turbomediator.handler.duration",
                unit: "ms",
                description: "Duration of message handler execution in milliseconds");
        }

        if (_options.EnableThroughputCounter)
        {
            _throughputCounter = _meter.CreateCounter<long>(
                "turbomediator.handler.requests",
                unit: "{request}",
                description: "Total number of message handler requests");
        }

        if (_options.EnableErrorCounter)
        {
            _errorCounter = _meter.CreateCounter<long>(
                "turbomediator.handler.errors",
                unit: "{error}",
                description: "Total number of message handler errors");
        }

        if (_options.EnableInFlightGauge)
        {
            _inFlightGauge = _meter.CreateUpDownCounter<int>(
                "turbomediator.handler.inflight",
                unit: "{request}",
                description: "Number of in-flight message handler requests");
        }
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var tags = BuildTags();
        var stopwatch = Stopwatch.StartNew();
        var succeeded = false;

        try
        {
            _inFlightGauge?.Add(1, tags);
            _throughputCounter?.Add(1, tags);

            var response = await next();
            succeeded = true;
            return response;
        }
        catch
        {
            var errorTags = BuildTags(isError: true);
            _errorCounter?.Add(1, errorTags);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            _inFlightGauge?.Add(-1, tags);

            var finalTags = BuildTags(succeeded: succeeded);
            _latencyHistogram?.Record(stopwatch.Elapsed.TotalMilliseconds, finalTags);
        }
    }

    private KeyValuePair<string, object?>[] BuildTags(bool succeeded = true, bool isError = false)
    {
        var tags = new List<KeyValuePair<string, object?>>();

        if (_options.IncludeMessageTypeLabel)
        {
            tags.Add(new KeyValuePair<string, object?>("message_type", _messageType));
        }

        if (_options.IncludeMessageCategoryLabel)
        {
            tags.Add(new KeyValuePair<string, object?>("message_category", _messageCategory));
        }

        if (_options.IncludeHandlerNameLabel)
        {
            tags.Add(new KeyValuePair<string, object?>("handler_name", $"Handler<{_messageType}, {typeof(TResponse).Name}>"));
        }

        if (_options.IncludeStatusLabel && !isError)
        {
            tags.Add(new KeyValuePair<string, object?>("status", succeeded ? "success" : "failure"));
        }

        if (isError)
        {
            tags.Add(new KeyValuePair<string, object?>("status", "error"));
        }

        // Add custom labels
        if (_options.CustomLabelValuesProvider != null)
        {
            var customValues = _options.CustomLabelValuesProvider();
            foreach (var kvp in customValues)
            {
                // If CustomLabels is configured, only include labels that are in the allowed list
                if (_options.CustomLabels.Count > 0 && !_options.CustomLabels.Contains(kvp.Key))
                {
                    continue;
                }
                tags.Add(new KeyValuePair<string, object?>(kvp.Key, kvp.Value));
            }
        }

        return tags.ToArray();
    }

    private string GetMessageCategory()
    {
        var messageType = typeof(TMessage);

        if (typeof(ICommand).IsAssignableFrom(messageType))
            return "command";
        if (typeof(IQuery<>).IsAssignableFrom(messageType) || messageType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuery<>)))
            return "query";
        if (typeof(INotification).IsAssignableFrom(messageType))
            return "notification";
        if (typeof(IRequest).IsAssignableFrom(messageType))
            return "request";

        return "unknown";
    }

    /// <summary>
    /// Disposes the metrics behavior and its meter.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _meter.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Static class providing shared metrics instances.
/// </summary>
public static class TurboMediatorMetrics
{
    private static Meter? _meter;
    private static Histogram<double>? _latencyHistogram;
    private static Counter<long>? _throughputCounter;
    private static Counter<long>? _errorCounter;
    private static UpDownCounter<int>? _inFlightGauge;

    /// <summary>
    /// Gets the shared meter instance.
    /// </summary>
    public static Meter Meter => _meter ??= new Meter("TurboMediator", "1.0.0");

    /// <summary>
    /// Gets the latency histogram.
    /// </summary>
    public static Histogram<double> LatencyHistogram => _latencyHistogram ??=
        Meter.CreateHistogram<double>(
            "turbomediator.handler.duration",
            unit: "ms",
            description: "Duration of message handler execution in milliseconds");

    /// <summary>
    /// Gets the throughput counter.
    /// </summary>
    public static Counter<long> ThroughputCounter => _throughputCounter ??=
        Meter.CreateCounter<long>(
            "turbomediator.handler.requests",
            unit: "{request}",
            description: "Total number of message handler requests");

    /// <summary>
    /// Gets the error counter.
    /// </summary>
    public static Counter<long> ErrorCounter => _errorCounter ??=
        Meter.CreateCounter<long>(
            "turbomediator.handler.errors",
            unit: "{error}",
            description: "Total number of message handler errors");

    /// <summary>
    /// Gets the in-flight gauge.
    /// </summary>
    public static UpDownCounter<int> InFlightGauge => _inFlightGauge ??=
        Meter.CreateUpDownCounter<int>(
            "turbomediator.handler.inflight",
            unit: "{request}",
            description: "Number of in-flight message handler requests");

    /// <summary>
    /// Records a successful message handling operation.
    /// </summary>
    public static void RecordSuccess(string messageType, string category, double durationMs)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("message_category", category),
            new KeyValuePair<string, object?>("status", "success")
        };

        LatencyHistogram.Record(durationMs, tags);
        ThroughputCounter.Add(1, tags);
    }

    /// <summary>
    /// Records a failed message handling operation.
    /// </summary>
    public static void RecordError(string messageType, string category, double durationMs, string errorType)
    {
        var tags = new[]
        {
            new KeyValuePair<string, object?>("message_type", messageType),
            new KeyValuePair<string, object?>("message_category", category),
            new KeyValuePair<string, object?>("status", "error"),
            new KeyValuePair<string, object?>("error_type", errorType)
        };

        LatencyHistogram.Record(durationMs, tags);
        ErrorCounter.Add(1, tags);
    }
}
