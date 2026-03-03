using System;
using System.Collections.Generic;

namespace TurboMediator.Enterprise.Authorization;

/// <summary>
/// Exception thrown when authorization fails.
/// </summary>
public class UnauthorizedException : Exception
{
    /// <summary>
    /// Gets the type of the message that failed authorization.
    /// </summary>
    public Type MessageType { get; }

    /// <summary>
    /// Gets the policy that failed, if any.
    /// </summary>
    public string? Policy { get; }

    /// <summary>
    /// Gets the required roles that were not met, if any.
    /// </summary>
    public string? RequiredRoles { get; }

    /// <summary>
    /// Gets the authentication scheme that was required but not matched, if any.
    /// </summary>
    public string? AuthenticationScheme { get; }

    /// <summary>
    /// Creates a new UnauthorizedException.
    /// </summary>
    public UnauthorizedException(Type messageType, string? policy = null, string? requiredRoles = null, string? authenticationScheme = null)
        : base(FormatMessage(messageType, policy, requiredRoles, authenticationScheme))
    {
        MessageType = messageType;
        Policy = policy;
        RequiredRoles = requiredRoles;
        AuthenticationScheme = authenticationScheme;
    }

    private static string FormatMessage(Type messageType, string? policy, string? requiredRoles, string? authenticationScheme)
    {
        var parts = new List<string> { $"Authorization failed for {messageType.Name}" };

        if (!string.IsNullOrEmpty(policy))
            parts.Add($"Policy: {policy}");

        if (!string.IsNullOrEmpty(requiredRoles))
            parts.Add($"Required roles: {requiredRoles}");

        if (!string.IsNullOrEmpty(authenticationScheme))
            parts.Add($"Required authentication scheme: {authenticationScheme}");

        return string.Join(". ", parts);
    }
}
