using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.Inbox;
using TurboMediator.Persistence.Outbox;
using TurboMediator.Persistence.Transaction;

namespace TurboMediator.Persistence;

/// <summary>
/// Extension methods for configuring TurboMediator persistence behaviors.
/// These are provider-agnostic and work with any ITransactionManager, IOutboxStore, or IAuditStore implementation.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    #region Transaction

    /// <summary>
    /// Adds transaction behavior for the specified message type.
    /// Requires an ITransactionManager implementation to be registered.
    /// </summary>
    /// <typeparam name="TMessage">The message type to wrap in a transaction.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for transaction options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithTransaction<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<TransactionOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new TransactionOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
        {
            var transactionManager = sp.GetRequiredService<ITransactionManager>();
            var opts = sp.GetService<TransactionOptions>() ?? options;
            return new TransactionBehavior<TMessage, TResponse>(transactionManager, opts);
        });

        return builder;
    }

    /// <summary>
    /// Adds transaction behavior for all commands (messages implementing ICommand).
    /// Requires an ITransactionManager implementation to be registered.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for transaction options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithTransactionForCommands(
        this TurboMediatorBuilder builder,
        Action<TransactionOptions>? configure = null)
    {
        var options = new TransactionOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        return builder;
    }

    #endregion

    #region Outbox

    /// <summary>
    /// Adds outbox behavior for reliable message delivery.
    /// Requires an IOutboxStore implementation to be registered.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for outbox options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithOutbox(
        this TurboMediatorBuilder builder,
        Action<OutboxOptions>? configure = null)
    {
        var options = new OutboxOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);

        return builder;
    }

    /// <summary>
    /// Adds outbox behavior with a custom outbox store implementation.
    /// </summary>
    /// <typeparam name="TOutboxStore">The outbox store implementation type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for outbox options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithOutbox<TOutboxStore>(
        this TurboMediatorBuilder builder,
        Action<OutboxOptions>? configure = null)
        where TOutboxStore : class, IOutboxStore
    {
        var options = new OutboxOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<IOutboxStore, TOutboxStore>();

        return builder;
    }

    /// <summary>
    /// Adds outbox support using a fluent builder pattern.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">The configuration action.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithOutbox(
        this TurboMediatorBuilder builder,
        Action<OutboxBuilder> configure)
    {
        var outboxBuilder = new OutboxBuilder(builder.Services);
        configure(outboxBuilder);
        outboxBuilder.Build();
        return builder;
    }

    #endregion

    #region Dead Letter

    /// <summary>
    /// Adds a dead letter handler for outbox messages that exceed max retry attempts.
    /// </summary>
    /// <typeparam name="THandler">The dead letter handler implementation type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithDeadLetterHandler<THandler>(
        this TurboMediatorBuilder builder)
        where THandler : class, IOutboxDeadLetterHandler
    {
        builder.Services.AddScoped<IOutboxDeadLetterHandler, THandler>();
        return builder;
    }

    #endregion

    #region Inbox

    /// <summary>
    /// Adds inbox (idempotency) behavior for at-most-once message processing.
    /// Requires an IInboxStore implementation to be registered.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for inbox options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithInbox(
        this TurboMediatorBuilder builder,
        Action<InboxOptions>? configure = null)
    {
        var options = new InboxOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(InboxBehavior<,>));

        if (options.EnableAutoCleanup)
        {
            builder.Services.AddHostedService<InboxProcessor>();
        }

        return builder;
    }

    /// <summary>
    /// Adds inbox behavior with a custom inbox store implementation.
    /// </summary>
    /// <typeparam name="TInboxStore">The inbox store implementation type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for inbox options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithInbox<TInboxStore>(
        this TurboMediatorBuilder builder,
        Action<InboxOptions>? configure = null)
        where TInboxStore : class, IInboxStore
    {
        var options = new InboxOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<IInboxStore, TInboxStore>();
        builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(InboxBehavior<,>));

        if (options.EnableAutoCleanup)
        {
            builder.Services.AddHostedService<InboxProcessor>();
        }

        return builder;
    }

    #endregion

    #region Audit

    /// <summary>
    /// Adds audit behavior for the specified message type.
    /// Requires an IAuditStore implementation to be registered.
    /// </summary>
    /// <typeparam name="TMessage">The message type to audit.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for audit options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithAudit<TMessage, TResponse>(
        this TurboMediatorBuilder builder,
        Action<AuditOptions>? configure = null)
        where TMessage : IMessage
    {
        var options = new AuditOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<IPipelineBehavior<TMessage, TResponse>>(sp =>
        {
            var auditStore = sp.GetRequiredService<IAuditStore>();
            var opts = sp.GetService<AuditOptions>() ?? options;
            return new AuditBehavior<TMessage, TResponse>(auditStore, opts);
        });

        return builder;
    }

    /// <summary>
    /// Adds audit behavior for all messages marked with [Auditable].
    /// Requires an IAuditStore implementation to be registered.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for audit options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithAuditForAll(
        this TurboMediatorBuilder builder,
        Action<AuditOptions>? configure = null)
    {
        var options = new AuditOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        return builder;
    }

    /// <summary>
    /// Adds audit behavior with a custom audit store implementation.
    /// </summary>
    /// <typeparam name="TAuditStore">The audit store implementation type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional configuration for audit options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithAudit<TAuditStore>(
        this TurboMediatorBuilder builder,
        Action<AuditOptions>? configure = null)
        where TAuditStore : class, IAuditStore
    {
        var options = new AuditOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);
        builder.Services.AddScoped<IAuditStore, TAuditStore>();
        builder.Services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));

        return builder;
    }

    #endregion

    #region Convenience

    /// <summary>
    /// Configures all persistence behaviors at once.
    /// Requires ITransactionManager, IOutboxStore, and IAuditStore implementations to be registered.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configureTransaction">Optional configuration for transaction options.</param>
    /// <param name="configureOutbox">Optional configuration for outbox options.</param>
    /// <param name="configureAudit">Optional configuration for audit options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithPersistence(
        this TurboMediatorBuilder builder,
        Action<TransactionOptions>? configureTransaction = null,
        Action<OutboxOptions>? configureOutbox = null,
        Action<AuditOptions>? configureAudit = null)
    {
        return builder
            .WithTransactionForCommands(configureTransaction)
            .WithOutbox(configureOutbox)
            .WithAuditForAll(configureAudit);
    }

    #endregion
}
