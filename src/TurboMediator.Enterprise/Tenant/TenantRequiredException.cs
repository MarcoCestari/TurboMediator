using System;

namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Exception thrown when a tenant context is required but not available.
/// </summary>
public class TenantRequiredException : Exception
{
    /// <summary>
    /// Gets the type of the message that required a tenant.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Creates a new TenantRequiredException.
    /// </summary>
    public TenantRequiredException(Type messageType)
        : base($"A tenant context is required for {messageType.Name} but none was provided.")
    {
        MessageType = messageType;
    }
}
