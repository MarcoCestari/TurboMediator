using System;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Resilience.CircuitBreaker;
using TurboMediator.Resilience.Fallback;
using TurboMediator.Resilience.Hedging;
using TurboMediator.Resilience.Retry;
using TurboMediator.Resilience.Timeout;

namespace TurboMediator.Resilience;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add resilience features.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds retry behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for retry options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithRetry<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<RetryOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new RetryOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<RetryBehavior<TMessage, TResponse>>();
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                sp.GetRequiredService<RetryBehavior<TMessage, TResponse>>());
        });
        return builder;
    }

    /// <summary>
    /// Adds timeout behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithTimeout<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        TimeSpan timeout)
        where TMessage : IMessage
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(new TimeoutBehavior<TMessage, TResponse>(timeout));
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                sp.GetRequiredService<TimeoutBehavior<TMessage, TResponse>>());
        });
        return builder;
    }

    /// <summary>
    /// Adds timeout behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="timeoutMilliseconds">The timeout in milliseconds.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithTimeout<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        int timeoutMilliseconds)
        where TMessage : IMessage
    {
        return builder.WithTimeout<TMessage, TResponse>(TimeSpan.FromMilliseconds(timeoutMilliseconds));
    }

    /// <summary>
    /// Adds circuit breaker behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for circuit breaker options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithCircuitBreaker<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<CircuitBreakerOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new CircuitBreakerOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(new CircuitBreakerBehavior<TMessage, TResponse>(options));
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                sp.GetRequiredService<CircuitBreakerBehavior<TMessage, TResponse>>());
        });
        return builder;
    }

    /// <summary>
    /// Adds fallback behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for fallback options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithFallback<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<FallbackOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new FallbackOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>, FallbackBehavior<TMessage, TResponse>>();
        });
        return builder;
    }

    /// <summary>
    /// Adds fallback behavior for a specific message type with a specific fallback handler.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="TFallback">The fallback handler type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for fallback options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithFallback<TMessage, TResponse, TFallback>(
        this TurboMediatorBuilder builder,
        Action<FallbackOptions>? configure = null)
        where TMessage : IMessage
        where TFallback : class, IFallbackHandler<TMessage, TResponse>
    {
        var options = new FallbackOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped<TFallback>();
            services.AddScoped<IFallbackHandler<TMessage, TResponse>, TFallback>();
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>, FallbackBehavior<TMessage, TResponse>>();
        });
        return builder;
    }

    /// <summary>
    /// Adds hedging behavior for a specific message type.
    /// Hedging sends parallel requests and uses the first successful response.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for hedging options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithHedging<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<HedgingOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new HedgingOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(new HedgingBehavior<TMessage, TResponse>(options));
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                sp.GetRequiredService<HedgingBehavior<TMessage, TResponse>>());
        });
        return builder;
    }

    /// <summary>
    /// Adds hedging behavior with the specified number of parallel attempts.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="maxParallelAttempts">The maximum number of parallel attempts.</param>
    /// <param name="delayMs">The delay in milliseconds between attempts.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithHedging<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        int maxParallelAttempts,
        int delayMs = 100)
        where TMessage : IMessage
    {
        return builder.WithHedging<TMessage, TResponse>(opt =>
        {
            opt.MaxParallelAttempts = maxParallelAttempts;
            opt.Delay = TimeSpan.FromMilliseconds(delayMs);
        });
    }
}
