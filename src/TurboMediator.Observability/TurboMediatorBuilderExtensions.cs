using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using TurboMediator.Observability.Correlation;
using TurboMediator.Observability.HealthChecks;
using TurboMediator.Observability.Logging;
using TurboMediator.Observability.Metrics;
using TurboMediator.Observability.Telemetry;

namespace TurboMediator.Observability;

/// <summary>
/// Extension methods for TurboMediatorBuilder to add observability features.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Adds telemetry behavior for OpenTelemetry integration.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for telemetry options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithTelemetry<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<TelemetryOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new TelemetryOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>, TelemetryBehavior<TMessage, TResponse>>();
        });
        return builder;
    }

    /// <summary>
    /// Adds telemetry behavior for all message types.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for telemetry options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithTelemetry(
        this TurboMediatorBuilder builder,
        Action<TelemetryOptions>? configure = null)
    {
        var options = new TelemetryOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TelemetryBehavior<,>));
        });
        return builder;
    }

    /// <summary>
    /// Adds mediator context support for context propagation.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithMediatorContext(this TurboMediatorBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IMediatorContextAccessor, MediatorContextAccessor>();
            services.AddScoped<IMediatorContext>(sp =>
            {
                var accessor = sp.GetRequiredService<IMediatorContextAccessor>();
                if (accessor.Context == null)
                {
                    accessor.Context = new MediatorContext();
                }
                return accessor.Context;
            });
        });
        return builder;
    }

    /// <summary>
    /// Adds correlation ID behavior for automatic correlation ID propagation.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for correlation options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithCorrelationId<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<CorrelationOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new CorrelationOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>, CorrelationIdBehavior<TMessage, TResponse>>();
        });

        // Ensure mediator context is registered
        builder.WithMediatorContext();

        return builder;
    }

    /// <summary>
    /// Adds correlation ID behavior for all message types.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for correlation options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithCorrelationId(
        this TurboMediatorBuilder builder,
        Action<CorrelationOptions>? configure = null)
    {
        var options = new CorrelationOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(CorrelationIdBehavior<,>));

            // Register the delegating handler for HttpClient propagation
            if (options.PropagateToHttpClient)
            {
                services.AddTransient<CorrelationIdDelegatingHandler>();
                services.ConfigureAll<Microsoft.Extensions.Http.HttpClientFactoryOptions>(httpOptions =>
                {
                    httpOptions.HttpMessageHandlerBuilderActions.Add(handlerBuilder =>
                    {
                        handlerBuilder.AdditionalHandlers.Add(
                            handlerBuilder.Services.GetRequiredService<CorrelationIdDelegatingHandler>());
                    });
                });
            }
        });

        // Ensure mediator context is registered
        builder.WithMediatorContext();

        return builder;
    }

    /// <summary>
    /// Adds structured logging behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for structured logging options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithStructuredLogging<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<StructuredLoggingOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new StructuredLoggingOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>, StructuredLoggingBehavior<TMessage, TResponse>>();
        });

        return builder;
    }

    /// <summary>
    /// Adds structured logging behavior for all message types.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for structured logging options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithStructuredLogging(
        this TurboMediatorBuilder builder,
        Action<StructuredLoggingOptions>? configure = null)
    {
        var options = new StructuredLoggingOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(StructuredLoggingBehavior<,>));
        });

        return builder;
    }

    /// <summary>
    /// Adds metrics behavior for a specific message type.
    /// </summary>
    /// <typeparam name="TMessage">The message type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for metrics options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithMetrics<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<MetricsOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new MetricsOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton(new MetricsBehavior<TMessage, TResponse>(options));
            services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
                sp.GetRequiredService<MetricsBehavior<TMessage, TResponse>>());
        });

        return builder;
    }

    /// <summary>
    /// Adds metrics behavior for all message types using System.Diagnostics.Metrics.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for metrics options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithMetrics(
        this TurboMediatorBuilder builder,
        Action<MetricsOptions>? configure = null)
    {
        var options = new MetricsOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddScoped(typeof(IPipelineBehavior<,>), typeof(MetricsBehavior<,>));
        });

        return builder;
    }

    /// <summary>
    /// Adds Prometheus-compatible metrics behavior.
    /// This is an alias for WithMetrics with Prometheus-friendly defaults.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for metrics options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithPrometheusMetrics(
        this TurboMediatorBuilder builder,
        Action<MetricsOptions>? configure = null)
    {
        return builder.WithMetrics(configure);
    }

    /// <summary>
    /// Adds TurboMediator health check to the service collection.
    /// </summary>
    /// <param name="builder">The builder.</param>
    /// <param name="configure">Optional configuration for health check options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithHealthCheck(
        this TurboMediatorBuilder builder,
        Action<TurboMediatorHealthCheckOptions>? configure = null)
    {
        var options = new TurboMediatorHealthCheckOptions();
        configure?.Invoke(options);

        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<TurboMediatorHealthCheck>();
            services.AddSingleton<IHealthCheck>(sp => sp.GetRequiredService<TurboMediatorHealthCheck>());
            services.AddSingleton(new HealthCheckRegistration("TurboMediator", options.Tags, options.Timeout));
        });

        return builder;
    }
}
