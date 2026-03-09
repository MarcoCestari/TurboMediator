using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TurboMediator.Persistence;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.EntityFramework.Audit;
using TurboMediator.Persistence.EntityFramework.Inbox;
using TurboMediator.Persistence.EntityFramework.Outbox;
using TurboMediator.Persistence.EntityFramework.Transaction;
using TurboMediator.Persistence.Inbox;
using TurboMediator.Persistence.Outbox;
using TurboMediator.Persistence.Transaction;

namespace TurboMediator.Persistence.EntityFramework;

/// <summary>
/// Extension methods for configuring TurboMediator Entity Framework Core provider.
/// Registers EF Core implementations of the Persistence abstractions.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Registers EF Core as the persistence provider for TurboMediator.
    /// Registers EfCoreTransactionManager, EfCoreOutboxStore, EfCoreInboxStore, and EfCoreAuditStore.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional action to configure persistence options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEntityFramework<TContext>(
        this TurboMediatorBuilder builder,
        Action<EfCorePersistenceOptions>? configure = null) where TContext : DbContext
    {
        var options = new EfCorePersistenceOptions();
        configure?.Invoke(options);

        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddScoped<ITransactionManager, EfCoreTransactionManager<TContext>>();
        builder.Services.TryAddScoped<IOutboxStore, EfCoreOutboxStore<TContext>>();
        builder.Services.TryAddScoped<IAuditStore, EfCoreAuditStore<TContext>>();
        builder.Services.TryAddScoped<IInboxStore, EfCoreInboxStore<TContext>>();
        return builder;
    }

    /// <summary>
    /// Registers EF Core as the transaction provider.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEfCoreTransactions<TContext>(this TurboMediatorBuilder builder) where TContext : DbContext
    {
        builder.Services.TryAddScoped<ITransactionManager, EfCoreTransactionManager<TContext>>();
        return builder;
    }

    /// <summary>
    /// Registers EF Core as the outbox store provider.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional action to configure persistence options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEfCoreOutboxStore<TContext>(
        this TurboMediatorBuilder builder,
        Action<EfCorePersistenceOptions>? configure = null) where TContext : DbContext
    {
        var options = new EfCorePersistenceOptions();
        configure?.Invoke(options);

        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddScoped<IOutboxStore, EfCoreOutboxStore<TContext>>();
        return builder;
    }

    /// <summary>
    /// Registers EF Core as the audit store provider.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional action to configure persistence options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEfCoreAuditStore<TContext>(
        this TurboMediatorBuilder builder,
        Action<EfCorePersistenceOptions>? configure = null) where TContext : DbContext
    {
        var options = new EfCorePersistenceOptions();
        configure?.Invoke(options);

        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddScoped<IAuditStore, EfCoreAuditStore<TContext>>();
        return builder;
    }

    /// <summary>
    /// Registers EF Core as the inbox store provider.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configure">Optional action to configure persistence options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEfCoreInboxStore<TContext>(
        this TurboMediatorBuilder builder,
        Action<EfCorePersistenceOptions>? configure = null) where TContext : DbContext
    {
        var options = new EfCorePersistenceOptions();
        configure?.Invoke(options);

        builder.Services.TryAddSingleton(options);
        builder.Services.TryAddScoped<IInboxStore, EfCoreInboxStore<TContext>>();
        return builder;
    }

    /// <summary>
    /// Configures all Entity Framework behaviors at once (convenience method).
    /// Registers EF Core provider + transaction, outbox, and audit behaviors.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configurePersistence">Optional configuration for persistence options.</param>
    /// <param name="configureTransaction">Optional configuration for transaction options.</param>
    /// <param name="configureOutbox">Optional configuration for outbox options.</param>
    /// <param name="configureAudit">Optional configuration for audit options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithEntityFramework<TContext>(
        this TurboMediatorBuilder builder,
        Action<EfCorePersistenceOptions>? configurePersistence = null,
        Action<TransactionOptions>? configureTransaction = null,
        Action<OutboxOptions>? configureOutbox = null,
        Action<AuditOptions>? configureAudit = null) where TContext : DbContext
    {
        return builder
            .UseEntityFramework<TContext>(configurePersistence)
            .WithPersistence(configureTransaction, configureOutbox, configureAudit);
    }
}

/// <summary>
/// Extension methods for OutboxBuilder to add EF Core store support.
/// </summary>
public static class OutboxBuilderEfCoreExtensions
{
    /// <summary>
    /// Uses the Entity Framework Core outbox store.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="outboxBuilder">The outbox builder.</param>
    /// <param name="configure">Optional action to configure persistence options.</param>
    /// <returns>The builder for chaining.</returns>
    public static OutboxBuilder UseEfCoreStore<TContext>(
        this OutboxBuilder outboxBuilder,
        Action<EfCorePersistenceOptions>? configure = null) where TContext : DbContext
    {
        var options = new EfCorePersistenceOptions();
        configure?.Invoke(options);

        outboxBuilder.Services.TryAddSingleton(options);
        outboxBuilder.Services.TryAddScoped<IOutboxStore, EfCoreOutboxStore<TContext>>();
        return outboxBuilder;
    }

    /// <summary>
    /// Uses the Entity Framework Core inbox store for idempotency.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="outboxBuilder">The outbox builder.</param>
    /// <param name="configure">Optional action to configure persistence options.</param>
    /// <returns>The builder for chaining.</returns>
    public static OutboxBuilder UseEfCoreInboxStore<TContext>(
        this OutboxBuilder outboxBuilder,
        Action<EfCorePersistenceOptions>? configure = null) where TContext : DbContext
    {
        var options = new EfCorePersistenceOptions();
        configure?.Invoke(options);

        outboxBuilder.Services.TryAddSingleton(options);
        outboxBuilder.Services.TryAddScoped<IInboxStore, EfCoreInboxStore<TContext>>();
        return outboxBuilder;
    }
}

/// <summary>
/// Extension methods for IServiceCollection to add EF Core persistence stores.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds EF Core persistence store support with default options.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEfCorePersistenceStore<TContext>(this IServiceCollection services)
        where TContext : DbContext
    {
        return services.AddEfCorePersistenceStore<TContext>(_ => { });
    }

    /// <summary>
    /// Adds EF Core persistence store support with custom options.
    /// </summary>
    /// <typeparam name="TContext">The DbContext type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEfCorePersistenceStore<TContext>(
        this IServiceCollection services,
        Action<EfCorePersistenceOptions> configure)
        where TContext : DbContext
    {
        var options = new EfCorePersistenceOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddScoped<ITransactionManager, EfCoreTransactionManager<TContext>>();
        services.TryAddScoped<IOutboxStore, EfCoreOutboxStore<TContext>>();
        services.TryAddScoped<IAuditStore, EfCoreAuditStore<TContext>>();
        services.TryAddScoped<IInboxStore, EfCoreInboxStore<TContext>>();

        return services;
    }
}
