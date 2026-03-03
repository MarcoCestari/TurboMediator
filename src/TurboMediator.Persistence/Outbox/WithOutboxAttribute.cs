using System;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Marks a notification to be persisted to the outbox before publishing.
/// This ensures reliable message delivery using the transactional outbox pattern.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class WithOutboxAttribute : Attribute
{
    /// <summary>
    /// Gets or sets whether to publish immediately after persisting to outbox.
    /// Default is false (background processor will handle publishing).
    /// </summary>
    public bool PublishImmediately { get; set; } = false;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;
}
