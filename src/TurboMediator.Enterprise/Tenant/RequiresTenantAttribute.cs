using System;

namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Attribute to mark a message as requiring a tenant context.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public class RequiresTenantAttribute : Attribute
{
}
