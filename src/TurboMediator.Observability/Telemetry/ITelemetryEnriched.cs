using System.Collections.Generic;

namespace TurboMediator.Observability.Telemetry;

/// <summary>
/// Interface for messages that can enrich telemetry with custom tags.
/// </summary>
public interface ITelemetryEnriched
{
    /// <summary>
    /// Gets custom tags to add to the telemetry span.
    /// </summary>
    IEnumerable<KeyValuePair<string, object?>> GetTelemetryTags();
}
