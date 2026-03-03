using System;
using System.Threading;
using System.Threading.Tasks;

namespace TurboMediator.Enterprise.Tenant;

/// <summary>
/// Pipeline behavior that enforces tenant context on messages.
/// </summary>
/// <typeparam name="TMessage">The type of the message.</typeparam>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public class TenantBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    private readonly ITenantContext _tenantContext;
    private readonly TenantBehaviorOptions _options;

    /// <summary>
    /// Creates a new TenantBehavior.
    /// </summary>
    /// <param name="tenantContext">The tenant context.</param>
    public TenantBehavior(ITenantContext tenantContext)
        : this(tenantContext, new TenantBehaviorOptions())
    {
    }

    /// <summary>
    /// Creates a new TenantBehavior with options.
    /// </summary>
    /// <param name="tenantContext">The tenant context.</param>
    /// <param name="options">The behavior options.</param>
    public TenantBehavior(ITenantContext tenantContext, TenantBehaviorOptions options)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageType = typeof(TMessage);

        // Check if tenant is required via attribute
        var requiresTenant = messageType.GetCustomAttributes(typeof(RequiresTenantAttribute), false).Length > 0;

        if (requiresTenant && !_tenantContext.HasTenant)
        {
            throw new TenantRequiredException(messageType);
        }

        // If message is tenant-aware, validate tenant matches context
        if (message is ITenantAware tenantAware && _options.ValidateTenantMatch)
        {
            if (_tenantContext.HasTenant &&
                !string.IsNullOrEmpty(tenantAware.TenantId) &&
                !string.Equals(_tenantContext.TenantId, tenantAware.TenantId, StringComparison.OrdinalIgnoreCase))
            {
                throw new TenantMismatchException(messageType, _tenantContext.TenantId, tenantAware.TenantId);
            }
        }

        return next();
    }
}
