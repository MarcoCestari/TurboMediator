using System;

namespace TurboMediator.Resilience.Timeout;

/// <summary>
/// Options for configuring timeout behavior.
/// </summary>
public class TimeoutOptions
{
    /// <summary>
    /// Gets or sets the default timeout duration applied when no <see cref="TimeoutAttribute"/> is specified.
    /// Defaults to 30 seconds.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
