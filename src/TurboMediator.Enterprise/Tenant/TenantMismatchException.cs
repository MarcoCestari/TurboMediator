using System;

namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Exception thrown when tenant context doesn't match the message's tenant.
/// </summary>
public class TenantMismatchException : Exception
{
    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Gets the expected tenant ID from the context.
    /// </summary>
    public string? ExpectedTenantId { get; }

    /// <summary>
    /// Gets the actual tenant ID from the message.
    /// </summary>
    public string? ActualTenantId { get; }

    /// <summary>
    /// Creates a new TenantMismatchException.
    /// </summary>
    public TenantMismatchException(Type messageType, string? expectedTenantId, string? actualTenantId)
        : base($"Tenant mismatch for {messageType.Name}. Expected: {expectedTenantId ?? "(none)"}, Actual: {actualTenantId ?? "(none)"}")
    {
        MessageType = messageType;
        ExpectedTenantId = expectedTenantId;
        ActualTenantId = actualTenantId;
    }
}
