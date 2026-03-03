using System;

namespace TurboMediator.Enterprise.Deduplication;

/// <summary>
/// Exception thrown when a duplicate request is detected and is still being processed.
/// </summary>
public class DuplicateRequestException : Exception
{
    /// <summary>
    /// Gets the idempotency key that was duplicated.
    /// </summary>
    public string IdempotencyKey { get; }

    /// <summary>
    /// Creates a new DuplicateRequestException.
    /// </summary>
    public DuplicateRequestException(string idempotencyKey)
        : base($"A request with idempotency key '{idempotencyKey}' is already being processed.")
    {
        IdempotencyKey = idempotencyKey;
    }
}
