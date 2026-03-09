namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Marks a notification to be persisted to the outbox before publishing.
/// This ensures reliable message delivery using the transactional outbox pattern.
/// Optionally specifies the destination (queue/topic) for message broker routing.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class WithOutboxAttribute : Attribute
{
    /// <summary>
    /// Creates a new WithOutboxAttribute without a specific destination.
    /// The router will use naming conventions or default destination.
    /// </summary>
    public WithOutboxAttribute() { }

    /// <summary>
    /// Creates a new WithOutboxAttribute with a specific destination (queue/topic).
    /// </summary>
    /// <param name="destination">The destination queue/topic name for broker publishing.</param>
    public WithOutboxAttribute(string destination)
    {
        Destination = destination ?? throw new ArgumentNullException(nameof(destination));
    }

    /// <summary>
    /// Gets the destination queue/topic name, or null if not specified.
    /// When null, the router uses naming conventions or the default destination.
    /// </summary>
    public string? Destination { get; }

    /// <summary>
    /// Gets or sets the optional partition key property name for ordered delivery.
    /// The value of this property on the message will be forwarded to the broker.
    /// </summary>
    public string? PartitionKey { get; set; }

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
