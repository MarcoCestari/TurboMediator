using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace TurboMediator;

/// <summary>
/// Builder for configuring TurboMediator with a fluent API.
/// </summary>
public sealed class TurboMediatorBuilder
{
    private readonly IServiceCollection _services;
    private readonly TurboMediatorOptions _options = new();
    private readonly List<Action<IServiceCollection>> _configurationActions = new();

    /// <summary>
    /// Creates a new TurboMediatorBuilder.
    /// </summary>
    /// <param name="services">The service collection.</param>
    public TurboMediatorBuilder(IServiceCollection services)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    /// <summary>
    /// Gets the configured options. Used by the generated code.
    /// </summary>
    public TurboMediatorOptions Options => _options;

    /// <summary>
    /// Gets the configuration actions to apply. Used by the generated code.
    /// </summary>
    public IReadOnlyList<Action<IServiceCollection>> ConfigurationActions => _configurationActions;

    #region Core Configuration

    /// <summary>
    /// Sets the notification publisher strategy.
    /// </summary>
    /// <param name="publisher">The notification publisher to use.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithNotificationPublisher(INotificationPublisher publisher)
    {
        _options.NotificationPublisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        return this;
    }

    /// <summary>
    /// Uses the sequential (ForeachAwait) notification publisher.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithSequentialNotifications()
    {
        _options.NotificationPublisher = ForeachAwaitPublisher.Instance;
        return this;
    }

    /// <summary>
    /// Uses the parallel (TaskWhenAll) notification publisher.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithParallelNotifications()
    {
        _options.NotificationPublisher = TaskWhenAllPublisher.Instance;
        return this;
    }

    /// <summary>
    /// Uses fire-and-forget notification publishing.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithFireAndForgetNotifications()
    {
        _options.NotificationPublisher = FireAndForgetPublisher.Instance;
        return this;
    }

    /// <summary>
    /// Sets whether to throw when no notification handler is found.
    /// </summary>
    /// <param name="shouldThrow">True to throw, false to silently ignore.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder ThrowOnNoNotificationHandler(bool shouldThrow = true)
    {
        _options.ThrowOnNoNotificationHandler = shouldThrow;
        return this;
    }

    #endregion

    #region Pipeline Behaviors

    /// <summary>
    /// Adds a pipeline behavior.
    /// </summary>
    /// <typeparam name="TBehavior">The type of pipeline behavior.</typeparam>
    /// <param name="lifetime">The service lifetime. Default is Singleton.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithPipelineBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TBehavior : class
    {
        _configurationActions.Add(services => RegisterBehavior<TBehavior>(services, typeof(IPipelineBehavior<,>), lifetime));
        return this;
    }

    /// <summary>
    /// Adds a pre-processor.
    /// </summary>
    /// <typeparam name="TPreProcessor">The type of pre-processor.</typeparam>
    /// <param name="lifetime">The service lifetime. Default is Singleton.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithPreProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPreProcessor>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TPreProcessor : class
    {
        _configurationActions.Add(services => RegisterBehavior<TPreProcessor>(services, typeof(IMessagePreProcessor<>), lifetime));
        return this;
    }

    /// <summary>
    /// Adds a post-processor.
    /// </summary>
    /// <typeparam name="TPostProcessor">The type of post-processor.</typeparam>
    /// <param name="lifetime">The service lifetime. Default is Singleton.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithPostProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPostProcessor>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TPostProcessor : class
    {
        _configurationActions.Add(services => RegisterBehavior<TPostProcessor>(services, typeof(IMessagePostProcessor<,>), lifetime));
        return this;
    }

    /// <summary>
    /// Adds an exception handler.
    /// </summary>
    /// <typeparam name="TExceptionHandler">The type of exception handler.</typeparam>
    /// <param name="lifetime">The service lifetime. Default is Singleton.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithExceptionHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TExceptionHandler>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TExceptionHandler : class
    {
        _configurationActions.Add(services => RegisterBehavior<TExceptionHandler>(services, typeof(IMessageExceptionHandler<,,>), lifetime));
        return this;
    }

    #endregion

    #region Stream Pipeline

    /// <summary>
    /// Adds a stream pipeline behavior.
    /// </summary>
    /// <typeparam name="TBehavior">The type of stream pipeline behavior.</typeparam>
    /// <param name="lifetime">The service lifetime. Default is Singleton.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithStreamPipelineBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBehavior>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TBehavior : class
    {
        _configurationActions.Add(services => RegisterBehavior<TBehavior>(services, typeof(IStreamPipelineBehavior<,>), lifetime));
        return this;
    }

    /// <summary>
    /// Adds a stream pre-processor.
    /// </summary>
    /// <typeparam name="TPreProcessor">The type of stream pre-processor.</typeparam>
    /// <param name="lifetime">The service lifetime. Default is Singleton.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithStreamPreProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPreProcessor>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TPreProcessor : class
    {
        _configurationActions.Add(services => RegisterBehavior<TPreProcessor>(services, typeof(IStreamPreProcessor<>), lifetime));
        return this;
    }

    /// <summary>
    /// Adds a stream post-processor.
    /// </summary>
    /// <typeparam name="TPostProcessor">The type of stream post-processor.</typeparam>
    /// <param name="lifetime">The service lifetime. Default is Singleton.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder WithStreamPostProcessor<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TPostProcessor>(ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TPostProcessor : class
    {
        _configurationActions.Add(services => RegisterBehavior<TPostProcessor>(services, typeof(IStreamPostProcessor<,>), lifetime));
        return this;
    }

    #endregion

    #region Custom Configuration

    /// <summary>
    /// Allows custom configuration of services.
    /// </summary>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        _configurationActions.Add(configure);
        return this;
    }

    /// <summary>
    /// Configures options directly.
    /// </summary>
    /// <param name="configure">The options configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public TurboMediatorBuilder ConfigureOptions(Action<TurboMediatorOptions> configure)
    {
        if (configure == null) throw new ArgumentNullException(nameof(configure));
        configure(_options);
        return this;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Registers a behavior type by scanning its interfaces and registering them.
    /// The <see cref="DynamicallyAccessedMembersAttribute"/> on TBehavior ensures the trimmer
    /// preserves the constructors needed for DI activation in Native AOT.
    /// </summary>
    internal static void RegisterBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TBehavior>(
        IServiceCollection services, Type interfaceType, ServiceLifetime lifetime)
        where TBehavior : class
    {
        var behaviorType = typeof(TBehavior);
        var matchingInterfaces = behaviorType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == interfaceType);

        foreach (var iface in matchingInterfaces)
        {
            services.Add(new ServiceDescriptor(iface, behaviorType, lifetime));
        }
    }

    #endregion

    /// <summary>
    /// Builds and applies all configurations.
    /// </summary>
    internal void Build()
    {
        // Register options
        _services.AddSingleton(_options);

        // Apply all configuration actions
        foreach (var action in _configurationActions)
        {
            action(_services);
        }
    }
}
