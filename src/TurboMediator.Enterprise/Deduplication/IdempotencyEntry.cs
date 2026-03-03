using System;

namespace TurboMediator.Enterprise.Deduplication;

/// <summary>
/// Represents a stored idempotency entry.
/// </summary>
/// <typeparam name="T">The type of the response.</typeparam>
public class IdempotencyEntry<T>
{
    /// <summary>
    /// Gets the stored response.
    /// </summary>
    public T Response { get; }

    /// <summary>
    /// Gets when this entry was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets when this entry expires.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Creates a new idempotency entry.
    /// </summary>
    public IdempotencyEntry(T response, DateTimeOffset createdAt, DateTimeOffset expiresAt)
    {
        Response = response;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
    }
}
