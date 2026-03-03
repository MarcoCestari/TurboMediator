using System;

namespace TurboMediator.Enterprise.Deduplication;

/// <summary>
/// Options for configuring deduplication behavior.
/// </summary>
public class DeduplicationOptions
{
    /// <summary>
    /// Gets or sets the time-to-live for idempotency entries.
    /// Default is 24 hours.
    /// </summary>
    public TimeSpan TimeToLive { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets whether to throw immediately when a duplicate is detected.
    /// If false, will wait for the original request to complete.
    /// Default is false.
    /// </summary>
    public bool ThrowOnDuplicate { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of retries when waiting for a duplicate request.
    /// Default is 5.
    /// </summary>
    public int MaxWaitRetries { get; set; } = 5;

    /// <summary>
    /// Gets or sets the interval between wait retries.
    /// Default is 200ms.
    /// </summary>
    public TimeSpan WaitRetryInterval { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets whether to release the idempotency lock on error.
    /// This allows the request to be retried with the same key.
    /// Default is true.
    /// </summary>
    public bool ReleaseOnError { get; set; } = true;
}
