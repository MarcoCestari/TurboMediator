using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Enterprise.Deduplication;

/// <summary>
/// Interface for storing and checking idempotency keys.
/// </summary>
public interface IIdempotencyStore
{
    /// <summary>
    /// Tries to acquire a lock on the idempotency key.
    /// Returns true if the key is new and locked, false if it already exists.
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="ttl">Time-to-live for the key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if acquired, false if key already exists.</returns>
    ValueTask<bool> TryAcquireAsync(string key, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the stored response for an idempotency key.
    /// </summary>
    /// <typeparam name="T">The type of the response.</typeparam>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored response, or null if not found.</returns>
    ValueTask<IdempotencyEntry<T>?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores the response for an idempotency key.
    /// </summary>
    /// <typeparam name="T">The type of the response.</typeparam>
    /// <param name="key">The idempotency key.</param>
    /// <param name="response">The response to store.</param>
    /// <param name="ttl">Time-to-live for the entry.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask SetAsync<T>(string key, T response, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases the lock on an idempotency key without storing a response.
    /// Used when the operation fails and should be retryable.
    /// </summary>
    /// <param name="key">The idempotency key.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    ValueTask ReleaseAsync(string key, CancellationToken cancellationToken = default);
}
