using System;

namespace TurboMediator.Enterprise.Authorization;

/// <summary>
/// Attribute to mark a message as requiring authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = true, Inherited = false)]
public class AuthorizeAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the policy name to evaluate. If empty, just checks for authenticated user.
    /// </summary>
    public string? Policy { get; set; }

    /// <summary>
    /// Gets or sets the roles required (comma-separated).
    /// </summary>
    public string? Roles { get; set; }

    /// <summary>
    /// Gets or sets the authentication schemes to use (comma-separated).
    /// </summary>
    public string? AuthenticationSchemes { get; set; }

    /// <summary>
    /// Creates a new AuthorizeAttribute.
    /// </summary>
    public AuthorizeAttribute() { }

    /// <summary>
    /// Creates a new AuthorizeAttribute with a policy.
    /// </summary>
    /// <param name="policy">The policy name to evaluate.</param>
    public AuthorizeAttribute(string policy)
    {
        Policy = policy;
    }
}
