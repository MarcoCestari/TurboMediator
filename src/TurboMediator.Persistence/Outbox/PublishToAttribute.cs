using System;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Specifies the destination (queue/topic) for outbox messages of this type.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = true)]
public sealed class PublishToAttribute : Attribute
{
    /// <summary>
    /// The destination queue/topic name.
    /// </summary>
    public string Destination { get; }

    /// <summary>
    /// Optional partition key property name for ordered delivery.
    /// </summary>
    public string? PartitionKey { get; set; }

    /// <summary>
    /// Creates a new PublishToAttribute.
    /// </summary>
    public PublishToAttribute(string destination)
    {
        Destination = destination ?? throw new ArgumentNullException(nameof(destination));
    }
}
