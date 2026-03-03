using System;

namespace TurboMediator.DistributedLocking;

/// <summary>
/// Represents an acquired distributed lock. Disposing this handle releases the lock.
/// </summary>
public interface IDistributedLockHandle : IAsyncDisposable
{
    /// <summary>
    /// The key of the acquired lock.
    /// </summary>
    string Key { get; }
}
