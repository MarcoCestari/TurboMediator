using System;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator.RateLimiting;

/// <summary>
/// Extension methods for configuring rate limiting and bulkhead behaviors.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds rate limiting behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to rate limit.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRateLimiting<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<RateLimitOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new RateLimitOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IPipelineBehavior<TMessage, TResponse>, RateLimitingBehavior<TMessage, TResponse>>();

        return builder;
    }

    /// <summary>
    /// Adds rate limiting behavior for all commands.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRateLimitingForCommands(
        this TurboMediatorBuilder builder,
        Action<RateLimitOptions>? configure = null)
    {
        var options = new RateLimitOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(RateLimitingBehavior<,>));

        return builder;
    }

    /// <summary>
    /// Adds global rate limiting for all messages.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithGlobalRateLimiting(
        this TurboMediatorBuilder builder,
        Action<RateLimitOptions> configure)
    {
        var options = new RateLimitOptions();
        configure(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(RateLimitingBehavior<,>));

        return builder;
    }

    /// <summary>
    /// Adds bulkhead isolation for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to isolate.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">Optional configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithBulkhead<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<BulkheadOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new BulkheadOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<IPipelineBehavior<TMessage, TResponse>, BulkheadBehavior<TMessage, TResponse>>();

        return builder;
    }

    /// <summary>
    /// Adds bulkhead isolation for all messages.
    /// </summary>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithGlobalBulkhead(
        this TurboMediatorBuilder builder,
        Action<BulkheadOptions> configure)
    {
        var options = new BulkheadOptions();
        configure(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(typeof(IPipelineBehavior<,>), typeof(BulkheadBehavior<,>));

        return builder;
    }

    /// <summary>
    /// Adds both rate limiting and bulkhead behaviors for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The type of message.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="configureRateLimit">Rate limit configuration.</param>
    /// <param name="configureBulkhead">Bulkhead configuration.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithThrottling<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<RateLimitOptions>? configureRateLimit = null,
        Action<BulkheadOptions>? configureBulkhead = null)
        where TMessage : IMessage
    {
        builder.WithRateLimiting<TMessage, TResponse>(configureRateLimit);
        builder.WithBulkhead<TMessage, TResponse>(configureBulkhead);

        return builder;
    }

    /// <summary>
    /// Adds rate limiting with sliding window algorithm.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to rate limit.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="maxRequests">Maximum requests in the window.</param>
    /// <param name="windowSeconds">Window size in seconds.</param>
    /// <param name="segmentsPerWindow">Number of segments for sliding window.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithSlidingWindowRateLimit<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        int maxRequests,
        int windowSeconds,
        int segmentsPerWindow = 4)
        where TMessage : IMessage
    {
        return builder.WithRateLimiting<TMessage, TResponse>(opt =>
        {
            opt.MaxRequests = maxRequests;
            opt.WindowSeconds = windowSeconds;
            opt.Algorithm = RateLimiterAlgorithm.SlidingWindow;
            opt.SegmentsPerWindow = segmentsPerWindow;
        });
    }

    /// <summary>
    /// Adds rate limiting with token bucket algorithm.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to rate limit.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="bucketSize">Maximum tokens in the bucket.</param>
    /// <param name="tokensPerPeriod">Tokens added each period.</param>
    /// <param name="replenishmentPeriodSeconds">Replenishment period in seconds.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithTokenBucketRateLimit<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        int bucketSize,
        int tokensPerPeriod,
        int replenishmentPeriodSeconds)
        where TMessage : IMessage
    {
        return builder.WithRateLimiting<TMessage, TResponse>(opt =>
        {
            opt.MaxRequests = bucketSize;
            opt.TokensPerPeriod = tokensPerPeriod;
            opt.WindowSeconds = replenishmentPeriodSeconds;
            opt.Algorithm = RateLimiterAlgorithm.TokenBucket;
        });
    }

    /// <summary>
    /// Adds per-user rate limiting.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to rate limit.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="maxRequestsPerUser">Maximum requests per user.</param>
    /// <param name="windowSeconds">Window size in seconds.</param>
    /// <param name="userIdProvider">Function to get the current user ID.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithPerUserRateLimit<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        int maxRequestsPerUser,
        int windowSeconds,
        Func<string?> userIdProvider)
        where TMessage : IMessage
    {
        return builder.WithRateLimiting<TMessage, TResponse>(opt =>
        {
            opt.MaxRequests = maxRequestsPerUser;
            opt.WindowSeconds = windowSeconds;
            opt.PerUser = true;
            opt.UserIdProvider = userIdProvider;
        });
    }

    /// <summary>
    /// Adds per-tenant rate limiting.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to rate limit.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The mediator builder.</param>
    /// <param name="maxRequestsPerTenant">Maximum requests per tenant.</param>
    /// <param name="windowSeconds">Window size in seconds.</param>
    /// <param name="tenantIdProvider">Function to get the current tenant ID.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithPerTenantRateLimit<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        int maxRequestsPerTenant,
        int windowSeconds,
        Func<string?> tenantIdProvider)
        where TMessage : IMessage
    {
        return builder.WithRateLimiting<TMessage, TResponse>(opt =>
        {
            opt.MaxRequests = maxRequestsPerTenant;
            opt.WindowSeconds = windowSeconds;
            opt.PerTenant = true;
            opt.TenantIdProvider = tenantIdProvider;
        });
    }
}
