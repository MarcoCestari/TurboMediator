using System;

namespace TurboMediator.DistributedLocking;

/// <summary>
/// Marks a message as requiring a distributed lock before its handler is executed.
/// The lock is automatically released when the handler completes.
/// </summary>
/// <remarks>
/// When the message implements <see cref="ILockKeyProvider"/>, the key returned by
/// <see cref="ILockKeyProvider.GetLockKey"/> is used for fine-grained locking (e.g., per entity id).
/// Otherwise the message type name is used, producing a global lock for that message type.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class DistributedLockAttribute : Attribute
{
    /// <summary>
    /// Optional prefix prepended to the lock key.
    /// When not set, the message type name is used as the prefix.
    /// </summary>
    public string? KeyPrefix { get; set; }

    /// <summary>
    /// Maximum time in seconds to wait for the lock to become available.
    /// Defaults to 30 seconds. Set to 0 to fail immediately if the lock is held.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// When <c>true</c> (default), a <see cref="DistributedLockException"/> is thrown if the lock
    /// cannot be acquired within <see cref="TimeoutSeconds"/>.
    /// When <c>false</c>, <c>default(TResponse)</c> is returned without executing the handler.
    /// </summary>
    public bool ThrowIfNotAcquired { get; set; } = true;
}
