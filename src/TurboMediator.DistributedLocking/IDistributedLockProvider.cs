using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.DistributedLocking;

/// <summary>
/// Abstraction for acquiring distributed locks.
/// Implement this interface to provide a custom distributed lock backend
/// (Redis, SQL Server, Azure Blob Storage, etc.).
/// </summary>
public interface IDistributedLockProvider
{
    /// <summary>
    /// Attempts to acquire a distributed lock for the given key.
    /// </summary>
    /// <param name="key">The unique lock identifier.</param>
    /// <param name="timeout">Maximum time to wait for the lock. Use <see cref="TimeSpan.Zero"/> to fail immediately.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// An <see cref="IDistributedLockHandle"/> if the lock was acquired, or <c>null</c> if the
    /// timeout elapsed before the lock became available.
    /// </returns>
    Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
