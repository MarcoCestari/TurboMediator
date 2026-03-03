using System;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Enterprise.Authorization;
using TurboMediator.Enterprise.Deduplication;
using TurboMediator.Enterprise.Tenant;

namespace TurboMediator.Enterprise;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add enterprise features.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    #region Authorization

    /// <summary>
    /// Adds authorization behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithAuthorization<TMessage, TResponse>(this TurboMediatorBuilder builder)
        where TMessage : IMessage
    {
        builder.ConfigureServices(services =>
        {
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>, AuthorizationBehavior<TMessage, TResponse>>();
        });
        return builder;
    }

    /// <summary>
    /// Adds the default user context.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithDefaultUserContext(this TurboMediatorBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IUserContext>(DefaultUserContext.Instance);
        });
        return builder;
    }

    /// <summary>
    /// Adds a custom user context.
    /// </summary>
    /// <typeparam name="TUserContext">The type of user context.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithUserContext<TUserContext>(this TurboMediatorBuilder builder)
        where TUserContext : class, IUserContext
    {
        builder.ConfigureServices(services =>
        {
            services.AddScoped<IUserContext, TUserContext>();
        });
        return builder;
    }

    /// <summary>
    /// Adds the default authorization policy provider.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for policies.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithAuthorizationPolicies(
        this TurboMediatorBuilder builder,
        Action<DefaultAuthorizationPolicyProvider>? configure = null)
    {
        builder.ConfigureServices(services =>
        {
            var provider = new DefaultAuthorizationPolicyProvider();
            configure?.Invoke(provider);
            services.AddSingleton<IAuthorizationPolicyProvider>(provider);
        });
        return builder;
    }

    #endregion

    #region Multi-Tenancy

    /// <summary>
    /// Adds multi-tenancy behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for tenant behavior options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithMultiTenancy<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<TenantBehaviorOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new TenantBehaviorOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                new TenantBehavior<TMessage, TResponse>(
                    sp.GetRequiredService<ITenantContext>(),
                    options));
        });
        return builder;
    }

    /// <summary>
    /// Adds a custom tenant context.
    /// </summary>
    /// <typeparam name="TTenantContext">The type of tenant context.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithTenantContext<TTenantContext>(this TurboMediatorBuilder builder)
        where TTenantContext : class, ITenantContext
    {
        builder.ConfigureServices(services =>
        {
            services.AddScoped<ITenantContext, TTenantContext>();
        });
        return builder;
    }

    #endregion

    #region Deduplication

    /// <summary>
    /// Adds deduplication behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for deduplication options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithDeduplication<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<DeduplicationOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new DeduplicationOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                new DeduplicationBehavior<TMessage, TResponse>(
                    sp.GetRequiredService<IIdempotencyStore>(),
                    options));
        });
        return builder;
    }

    /// <summary>
    /// Adds an in-memory idempotency store.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithInMemoryIdempotencyStore(this TurboMediatorBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        });
        return builder;
    }

    #endregion
}
