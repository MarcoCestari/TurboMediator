using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TurboMediator.Persistence.Inbox;

namespace TurboMediator.Persistence.Outbox;

/// <summary>
/// Builder for configuring the outbox pattern with a fluent API.
/// Provider-agnostic: requires explicit store configuration via UseStore&lt;T&gt;().
/// </summary>
public sealed class OutboxBuilder
{
    private readonly IServiceCollection _services;
    private readonly OutboxOptions _outboxOptions = new();
    private readonly OutboxProcessorOptions _processorOptions = new();
    private readonly OutboxRoutingOptions _routingOptions = new();
    private readonly InboxOptions _inboxOptions = new();
    private bool _addProcessor;
    private bool _addRouter;
    private bool _addInbox;
    private Type? _customStoreType;
    private Type? _deadLetterHandlerType;
    private Type? _inboxStoreType;

    internal OutboxBuilder(IServiceCollection services)
    {
        _services = services;
    }

    /// <summary>
    /// Gets the underlying service collection.
    /// </summary>
    public IServiceCollection Services => _services;

    #region Store Configuration

    /// <summary>
    /// Uses a custom outbox store implementation.
    /// </summary>
    /// <typeparam name="TStore">The store type implementing IOutboxStore.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder UseStore<TStore>()
        where TStore : class, IOutboxStore
    {
        _customStoreType = typeof(TStore);
        return this;
    }

    #endregion

    #region Processor Configuration

    /// <summary>
    /// Adds the background processor for processing outbox messages.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder AddProcessor()
    {
        _addProcessor = true;
        return this;
    }

    /// <summary>
    /// Sets the processing interval.
    /// </summary>
    /// <param name="interval">The interval between processing batches.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithProcessingInterval(TimeSpan interval)
    {
        _processorOptions.ProcessingInterval = interval;
        return this;
    }

    /// <summary>
    /// Sets the batch size for processing.
    /// </summary>
    /// <param name="batchSize">The number of messages to process per batch.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithBatchSize(int batchSize)
    {
        _processorOptions.BatchSize = batchSize;
        return this;
    }

    /// <summary>
    /// Sets the maximum retry attempts for failed messages.
    /// </summary>
    /// <param name="maxRetries">The maximum number of retries.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithMaxRetries(int maxRetries)
    {
        _processorOptions.MaxRetryAttempts = maxRetries;
        return this;
    }

    /// <summary>
    /// Enables automatic cleanup of processed messages.
    /// </summary>
    /// <param name="cleanupAge">How old messages must be before cleanup.</param>
    /// <param name="cleanupInterval">How often to run cleanup.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithAutoCleanup(TimeSpan? cleanupAge = null, TimeSpan? cleanupInterval = null)
    {
        _processorOptions.EnableAutoCleanup = true;
        if (cleanupAge.HasValue)
            _processorOptions.CleanupAge = cleanupAge.Value;
        if (cleanupInterval.HasValue)
            _processorOptions.CleanupInterval = cleanupInterval.Value;
        return this;
    }

    /// <summary>
    /// Enables publishing to external message broker.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder PublishToMessageBroker()
    {
        _processorOptions.PublishToMessageBroker = true;
        return this;
    }

    #endregion

    #region Routing Configuration

    /// <summary>
    /// Adds message routing support.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder AddRouting()
    {
        _addRouter = true;
        return this;
    }

    /// <summary>
    /// Configures routing with a default destination.
    /// </summary>
    /// <param name="defaultDestination">The default destination for messages without explicit routing.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithDefaultDestination(string defaultDestination)
    {
        _addRouter = true;
        _routingOptions.DefaultDestination = defaultDestination;
        return this;
    }

    /// <summary>
    /// Configures routing with a destination prefix.
    /// </summary>
    /// <param name="prefix">The prefix to add to all destinations.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithDestinationPrefix(string prefix)
    {
        _addRouter = true;
        _routingOptions.DestinationPrefix = prefix;
        return this;
    }

    /// <summary>
    /// Uses kebab-case naming convention for destinations.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder UseKebabCaseNaming()
    {
        _addRouter = true;
        _routingOptions.NamingConvention = OutboxNamingConvention.KebabCase;
        return this;
    }

    /// <summary>
    /// Uses snake_case naming convention for destinations.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder UseSnakeCaseNaming()
    {
        _addRouter = true;
        _routingOptions.NamingConvention = OutboxNamingConvention.SnakeCase;
        return this;
    }

    /// <summary>
    /// Uses type name as destination.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder UseTypeNameRouting()
    {
        _addRouter = true;
        _routingOptions.NamingConvention = OutboxNamingConvention.TypeName;
        return this;
    }

    /// <summary>
    /// Maps a message type to a specific destination.
    /// </summary>
    /// <typeparam name="T">The message type.</typeparam>
    /// <param name="destination">The destination for this message type.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder MapType<T>(string destination)
    {
        _addRouter = true;
        _routingOptions.MapType<T>(destination);
        return this;
    }

    /// <summary>
    /// Maps a message type name to a specific destination.
    /// </summary>
    /// <param name="typeName">The message type name.</param>
    /// <param name="destination">The destination.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder MapType(string typeName, string destination)
    {
        _addRouter = true;
        _routingOptions.MapType(typeName, destination);
        return this;
    }

    #endregion

    #region Dead Letter Configuration

    /// <summary>
    /// Adds a dead letter handler for messages that exceed max retry attempts.
    /// </summary>
    /// <typeparam name="THandler">The dead letter handler type implementing IOutboxDeadLetterHandler.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithDeadLetterHandler<THandler>()
        where THandler : class, IOutboxDeadLetterHandler
    {
        _deadLetterHandlerType = typeof(THandler);
        return this;
    }

    #endregion

    #region Inbox Configuration

    /// <summary>
    /// Adds inbox (idempotency) support for at-most-once message processing.
    /// </summary>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithInbox()
    {
        _addInbox = true;
        return this;
    }

    /// <summary>
    /// Adds inbox support with a custom inbox store implementation.
    /// </summary>
    /// <typeparam name="TStore">The inbox store type implementing IInboxStore.</typeparam>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithInbox<TStore>()
        where TStore : class, IInboxStore
    {
        _addInbox = true;
        _inboxStoreType = typeof(TStore);
        return this;
    }

    /// <summary>
    /// Configures the inbox retention period for cleanup.
    /// </summary>
    /// <param name="retentionPeriod">How long to keep inbox records.</param>
    /// <returns>The builder for chaining.</returns>
    public OutboxBuilder WithInboxRetention(TimeSpan retentionPeriod)
    {
        _addInbox = true;
        _inboxOptions.RetentionPeriod = retentionPeriod;
        return this;
    }

    #endregion

    /// <summary>
    /// Builds and registers all configured outbox services.
    /// </summary>
    internal void Build()
    {
        // Register outbox options
        _services.AddSingleton(_outboxOptions);

        // Register store
        if (_customStoreType != null)
        {
            _services.TryAddScoped(typeof(IOutboxStore), _customStoreType);
        }

        // Register processor if configured
        if (_addProcessor)
        {
            // Use OutboxOptions as defaults if processor options weren't explicitly configured
            if (_processorOptions.BatchSize == 100 && _outboxOptions.BatchSize != 100)
                _processorOptions.BatchSize = _outboxOptions.BatchSize;
            if (_processorOptions.ProcessingInterval == TimeSpan.FromSeconds(5) && _outboxOptions.ProcessingInterval != TimeSpan.FromSeconds(5))
                _processorOptions.ProcessingInterval = _outboxOptions.ProcessingInterval;
            if (_processorOptions.CleanupAge == TimeSpan.FromDays(7) && _outboxOptions.RetentionPeriod != TimeSpan.FromDays(7))
                _processorOptions.CleanupAge = _outboxOptions.RetentionPeriod;
            if (_processorOptions.MaxRetryAttempts == 3 && _outboxOptions.MaxRetries != 3)
                _processorOptions.MaxRetryAttempts = _outboxOptions.MaxRetries;

            _services.AddSingleton(_processorOptions);
            _services.AddHostedService<OutboxProcessor>();
        }

        // Register router if configured
        if (_addRouter)
        {
            _services.AddSingleton(_routingOptions);
            _services.AddSingleton<IOutboxMessageRouter, OutboxMessageRouter>();
        }

        // Register dead letter handler if configured
        if (_deadLetterHandlerType != null)
        {
            _services.TryAddScoped(typeof(IOutboxDeadLetterHandler), _deadLetterHandlerType);
        }

        // Register inbox if configured
        if (_addInbox)
        {
            _services.AddSingleton(_inboxOptions);

            if (_inboxStoreType != null)
            {
                _services.TryAddScoped(typeof(IInboxStore), _inboxStoreType);
            }

            _services.AddScoped(typeof(IPipelineBehavior<,>), typeof(InboxBehavior<,>));

            if (_inboxOptions.EnableAutoCleanup)
            {
                _services.AddHostedService<InboxProcessor>();
            }
        }
    }
}
