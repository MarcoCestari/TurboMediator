using System;

namespace TurboMediator.Enterprise.Authorization;

/// <summary>
/// Attribute to allow anonymous access to a message, overriding any authorization requirements.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public class AllowAnonymousAttribute : Attribute
{
}
