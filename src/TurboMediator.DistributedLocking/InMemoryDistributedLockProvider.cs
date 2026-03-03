using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.DistributedLocking;

/// <summary>
/// A simple in-process distributed lock provider backed by <see cref="SemaphoreSlim"/>.
/// Suitable for development, testing, and single-node deployments.
/// <para>
/// <b>Not suitable for multi-instance distributed scenarios.</b>
/// For production distributed locking, use a provider backed by Redis, SQL Server, or another
/// shared coordination service.
/// </para>
/// </summary>
public sealed class InMemoryDistributedLockProvider : IDistributedLockProvider, IDisposable
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new(StringComparer.Ordinal);
    private bool _disposed;

    /// <inheritdoc />
    public async Task<IDistributedLockHandle?> TryAcquireAsync(
        string key,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var semaphore = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        var acquired = await semaphore.WaitAsync(timeout, cancellationToken);

        return acquired ? new InMemoryLockHandle(key, semaphore) : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var semaphore in _semaphores.Values)
            semaphore.Dispose();

        _semaphores.Clear();
    }

    private sealed class InMemoryLockHandle : IDistributedLockHandle
    {
        private readonly SemaphoreSlim _semaphore;
        private bool _released;

        public InMemoryLockHandle(string key, SemaphoreSlim semaphore)
        {
            Key = key;
            _semaphore = semaphore;
        }

        public string Key { get; }

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _semaphore.Release();
            }
            return ValueTask.CompletedTask;
        }
    }
}
