using System;

namespace TurboMediator.Scheduling;

/// <summary>
/// Global options for the scheduling system.
/// </summary>
public sealed class SchedulingOptions
{
    /// <summary>
    /// How often the processor polls the store for due jobs. Default: 10 seconds.
    /// </summary>
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);
}
