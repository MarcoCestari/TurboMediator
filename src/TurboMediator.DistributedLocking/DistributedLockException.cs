using System;

namespace TurboMediator.DistributedLocking;

/// <summary>
/// Exception thrown when a distributed lock cannot be acquired within the specified timeout.
/// </summary>
public sealed class DistributedLockException : Exception
{
    /// <summary>
    /// The lock key that could not be acquired.
    /// </summary>
    public string LockKey { get; }

    /// <summary>
    /// The timeout that was exhausted.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="DistributedLockException"/>.
    /// </summary>
    public DistributedLockException(string lockKey, TimeSpan timeout)
        : base($"Could not acquire distributed lock '{lockKey}' within {timeout.TotalSeconds:0.#}s.")
    {
        LockKey = lockKey;
        Timeout = timeout;
    }
}
