using System;

namespace TurboMediator.Observability.Correlation;

/// <summary>
/// Options for configuring correlation ID behavior.
/// </summary>
public class CorrelationOptions
{
    /// <summary>
    /// Gets or sets the HTTP header name for the correlation ID.
    /// Default is "X-Correlation-ID".
    /// </summary>
    public string HeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Gets or sets whether to generate a correlation ID if one is not present.
    /// Default is true.
    /// </summary>
    public bool GenerateIfMissing { get; set; } = true;

    /// <summary>
    /// Gets or sets the factory function for generating correlation IDs.
    /// Default generates a GUID without dashes.
    /// </summary>
    public Func<string> CorrelationIdGenerator { get; set; } = () => Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets or sets whether to add correlation ID to Activity baggage.
    /// Default is true.
    /// </summary>
    public bool AddToActivityBaggage { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to add correlation ID to log scope.
    /// Default is true.
    /// </summary>
    public bool AddToLogScope { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to propagate correlation ID to HttpClient requests.
    /// Default is true.
    /// </summary>
    public bool PropagateToHttpClient { get; set; } = true;

    /// <summary>
    /// Gets or sets the provider function to get the current correlation ID from external sources (e.g., HTTP context).
    /// </summary>
    public Func<string?>? CorrelationIdProvider { get; set; }
}
