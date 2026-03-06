using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Enterprise.Authorization;

/// <summary>
/// Pipeline behavior that enforces authorization on messages.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class AuthorizationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly IUserContext _userContext;
    private readonly IAuthorizationPolicyProvider? _policyProvider;

    /// <summary>
    /// Creates a new AuthorizationBehavior.
    /// </summary>
    /// <param name="userContext">The user context.</param>
    /// <param name="policyProvider">Optional policy provider for custom policies.</param>
    public AuthorizationBehavior(IUserContext userContext, IAuthorizationPolicyProvider? policyProvider = null)
    {
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _policyProvider = policyProvider;
    }

    /// <inheritdoc />
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage);

        // Check for AllowAnonymous
        if (messageType.GetCustomAttributes(typeof(AllowAnonymousAttribute), false).Length > 0)
        {
            return await next(message, cancellationToken);
        }

        // Get authorization attributes
        var authAttributes = messageType.GetCustomAttributes(typeof(AuthorizeAttribute), false)
            .OfType<AuthorizeAttribute>()
            .ToList();

        // If no authorization required, proceed
        if (authAttributes.Count == 0)
        {
            return await next(message, cancellationToken);
        }

        // Check if user is authenticated
        if (!_userContext.IsAuthenticated || _userContext.User == null)
        {
            throw new UnauthorizedException(messageType);
        }

        var user = _userContext.User;

        // Evaluate each authorization attribute
        foreach (var attr in authAttributes)
        {
            // Check roles
            if (!string.IsNullOrEmpty(attr.Roles))
            {
                var requiredRoles = attr.Roles.Split(',').Select(r => r.Trim());
                var hasRole = requiredRoles.Any(role => user.IsInRole(role));
                if (!hasRole)
                {
                    throw new UnauthorizedException(messageType, requiredRoles: attr.Roles);
                }
            }

            // Check policy
            if (!string.IsNullOrEmpty(attr.Policy))
            {
                if (_policyProvider == null)
                {
                    throw new UnauthorizedException(messageType, policy: attr.Policy);
                }

                var isAuthorized = await _policyProvider.EvaluatePolicyAsync(user, attr.Policy, cancellationToken);
                if (!isAuthorized)
                {
                    throw new UnauthorizedException(messageType, policy: attr.Policy);
                }
            }

            // Check authentication schemes
            if (!string.IsNullOrEmpty(attr.AuthenticationSchemes))
            {
                var requiredSchemes = attr.AuthenticationSchemes.Split(',').Select(s => s.Trim());
                var userScheme = user.Identity?.AuthenticationType;
                var hasMatchingScheme = requiredSchemes.Any(scheme =>
                    string.Equals(scheme, userScheme, StringComparison.OrdinalIgnoreCase));

                if (!hasMatchingScheme)
                {
                    throw new UnauthorizedException(messageType, authenticationScheme: attr.AuthenticationSchemes);
                }
            }
        }

        return await next(message, cancellationToken);
    }
}
