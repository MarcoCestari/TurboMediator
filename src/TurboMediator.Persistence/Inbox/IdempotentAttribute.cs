namespace TurboMediator.Persistence.Inbox;

/// <summary>
/// Marks a message handler for idempotent processing via the inbox pattern.
/// Messages with the same idempotency key will be processed at most once per handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class IdempotentAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the property to use as the idempotency key.
    /// If null, the message's GetHashCode() or serialized content hash will be used.
    /// </summary>
    public string? KeyProperty { get; set; }
}
