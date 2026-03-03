using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Enterprise.Deduplication;

/// <summary>
/// In-memory implementation of IIdempotencyStore.
/// Suitable for single-instance applications or testing.
/// </summary>
public class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyRecord> _store = new();
    private readonly object _cleanupLock = new();
    private DateTimeOffset _lastCleanup = DateTimeOffset.UtcNow;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    private class IdempotencyRecord
    {
        public object? Response { get; set; }
        public bool IsComplete { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }

    /// <inheritdoc />
    public ValueTask<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        CleanupIfNeeded();

        var now = DateTimeOffset.UtcNow;
        var record = new IdempotencyRecord
        {
            IsComplete = false,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl)
        };

        // Try to add the record, only succeeds if key doesn't exist
        if (_store.TryAdd(key, record))
        {
            return new ValueTask<bool>(true);
        }

        // Key exists, check if it's expired
        if (_store.TryGetValue(key, out var existing) && existing.ExpiresAt < now)
        {
            // Expired, try to replace
            if (_store.TryUpdate(key, record, existing))
            {
                return new ValueTask<bool>(true);
            }
        }

        return new ValueTask<bool>(false);
    }

    /// <inheritdoc />
    public ValueTask<IdempotencyEntry<T>?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var record) && record.IsComplete)
        {
            var now = DateTimeOffset.UtcNow;
            if (record.ExpiresAt > now)
            {
                var entry = new IdempotencyEntry<T>(
                    (T)record.Response!,
                    record.CreatedAt,
                    record.ExpiresAt);
                return new ValueTask<IdempotencyEntry<T>?>(entry);
            }
        }

        return new ValueTask<IdempotencyEntry<T>?>((IdempotencyEntry<T>?)null);
    }

    /// <inheritdoc />
    public ValueTask SetAsync<T>(string key, T response, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (_store.TryGetValue(key, out var existing))
        {
            existing.Response = response;
            existing.IsComplete = true;
            existing.ExpiresAt = now.Add(ttl);
        }
        else
        {
            _store[key] = new IdempotencyRecord
            {
                Response = response,
                IsComplete = true,
                CreatedAt = now,
                ExpiresAt = now.Add(ttl)
            };
        }

        return default;
    }

    /// <inheritdoc />
    public ValueTask ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.TryRemove(key, out _);
        return default;
    }

    private void CleanupIfNeeded()
    {
        var now = DateTimeOffset.UtcNow;
        if (now - _lastCleanup < _cleanupInterval)
        {
            return;
        }

        lock (_cleanupLock)
        {
            if (now - _lastCleanup < _cleanupInterval)
            {
                return;
            }

            var keysToRemove = new List<string>();
            foreach (var kvp in _store)
            {
                if (kvp.Value.ExpiresAt < now)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _store.TryRemove(key, out _);
            }

            _lastCleanup = now;
        }
    }
}
