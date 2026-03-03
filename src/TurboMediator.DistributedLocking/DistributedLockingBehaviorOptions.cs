using System;

namespace TurboMediator.DistributedLocking;

/// <summary>
/// Global defaults for the distributed locking behavior.
/// Per-message settings defined on <see cref="DistributedLockAttribute"/> always take precedence.
/// </summary>
public sealed class DistributedLockingBehaviorOptions
{
    /// <summary>
    /// Default timeout for acquiring a lock when <see cref="DistributedLockAttribute.TimeoutSeconds"/>
    /// is not overridden. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Global key prefix prepended to all lock keys before the per-message prefix.
    /// Useful for namespace isolation in shared Redis instances.
    /// Defaults to <c>null</c> (no global prefix).
    /// </summary>
    public string? GlobalKeyPrefix { get; set; }

    /// <summary>
    /// When <c>true</c>, a <see cref="DistributedLockException"/> is thrown globally when the lock cannot
    /// be acquired, unless overridden per message via <see cref="DistributedLockAttribute.ThrowIfNotAcquired"/>.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool DefaultThrowIfNotAcquired { get; set; } = true;
}
