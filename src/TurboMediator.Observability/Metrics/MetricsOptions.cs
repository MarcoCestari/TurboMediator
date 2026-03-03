using System;
using System.Collections.Generic;

namespace TurboMediator.Observability.Metrics;

/// <summary>
/// Options for configuring metrics collection.
/// </summary>
public class MetricsOptions
{
    /// <summary>
    /// Gets or sets whether to enable latency histogram.
    /// Default is true.
    /// </summary>
    public bool EnableLatencyHistogram { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable throughput counter.
    /// Default is true.
    /// </summary>
    public bool EnableThroughputCounter { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable error counter.
    /// Default is true.
    /// </summary>
    public bool EnableErrorCounter { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to enable in-flight gauge.
    /// Default is true.
    /// </summary>
    public bool EnableInFlightGauge { get; set; } = true;

    /// <summary>
    /// Gets or sets the histogram buckets for latency measurements (in milliseconds).
    /// </summary>
    /// <remarks>
    /// These values serve as documentation of the recommended buckets. The .NET Metrics API
    /// does not support passing bucket boundaries at instrument creation time — bucket configuration
    /// is done at the SDK/exporter level (e.g., via OTel SDK Views).
    /// <para>
    /// Example using OpenTelemetry SDK:
    /// <code>
    /// builder.Services.AddOpenTelemetry()
    ///     .WithMetrics(metrics => metrics
    ///         .AddView("turbomediator.handler.duration",
    ///             new ExplicitBucketHistogramConfiguration
    ///             {
    ///                 Boundaries = metricsOptions.LatencyBuckets
    ///             }));
    /// </code>
    /// </para>
    /// </remarks>
    public double[] LatencyBuckets { get; set; } = new double[]
    {
        1.0, 5.0, 10.0, 25.0, 50.0, 75.0, 100.0, 250.0, 500.0, 750.0, 1000.0, 2500.0, 5000.0, 7500.0, 10000.0
    };

    /// <summary>
    /// Gets or sets custom labels to add to all metrics.
    /// </summary>
    public IList<string> CustomLabels { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets whether to include message type as a label.
    /// Default is true.
    /// </summary>
    public bool IncludeMessageTypeLabel { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include handler name as a label.
    /// Default is false (can cause high cardinality).
    /// </summary>
    public bool IncludeHandlerNameLabel { get; set; } = false;

    /// <summary>
    /// Gets or sets whether to include message category as a label.
    /// Default is true.
    /// </summary>
    public bool IncludeMessageCategoryLabel { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to include success/failure status as a label.
    /// Default is true.
    /// </summary>
    public bool IncludeStatusLabel { get; set; } = true;

    /// <summary>
    /// Gets or sets the meter name.
    /// Default is "TurboMediator".
    /// </summary>
    public string MeterName { get; set; } = "TurboMediator";

    /// <summary>
    /// Gets or sets the meter version.
    /// Default is "1.0.0".
    /// </summary>
    public string MeterVersion { get; set; } = "1.0.0";

    /// <summary>
    /// Gets or sets a provider for custom label values.
    /// </summary>
    public Func<IReadOnlyDictionary<string, object?>>? CustomLabelValuesProvider { get; set; }
}
