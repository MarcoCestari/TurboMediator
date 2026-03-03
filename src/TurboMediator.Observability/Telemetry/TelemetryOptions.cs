namespace TurboMediator.Observability.Telemetry;

/// <summary>
/// Options for configuring telemetry behavior.
/// </summary>
public class TelemetryOptions
{
    /// <summary>
    /// Gets or sets whether to record traces (spans/activities). Default is true.
    /// </summary>
    public bool RecordTraces { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to record metrics. Default is true.
    /// </summary>
    public bool RecordMetrics { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to record exception stack traces. Default is false for performance.
    /// </summary>
    public bool RecordExceptionStackTrace { get; set; } = false;
}
