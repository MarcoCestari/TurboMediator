using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TurboMediator.Persistence;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.EF.Audit;
using TurboMediator.Persistence.EF.Inbox;
using TurboMediator.Persistence.EF.Outbox;
using TurboMediator.Persistence.EF.Transaction;
using TurboMediator.Persistence.Inbox;
using TurboMediator.Persistence.Outbox;
using TurboMediator.Persistence.Transaction;

namespace TurboMediator.Persistence.EF;

/// <summary>
/// Extension methods for configuring TurboMediator Entity Framework Core provider.
/// Registers EF Core implementations of the Persistence abstractions.
/// </summary>
public static class TurboMediatorBuilderExtensions
{
    /// <summary>
    /// Registers EF Core as the persistence provider for TurboMediator.
    /// Registers EfCoreTransactionManager, EfCoreOutboxStore, and EfCoreAuditStore.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEntityFramework(this TurboMediatorBuilder builder)
    {
        builder.Services.TryAddScoped<ITransactionManager, EfCoreTransactionManager>();
        builder.Services.TryAddScoped<IOutboxStore, EfCoreOutboxStore>();
        builder.Services.TryAddScoped<IAuditStore, EfCoreAuditStore>();
        builder.Services.TryAddScoped<IInboxStore, EfCoreInboxStore>();
        return builder;
    }

    /// <summary>
    /// Registers EF Core as the transaction provider.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEfCoreTransactions(this TurboMediatorBuilder builder)
    {
        builder.Services.TryAddScoped<ITransactionManager, EfCoreTransactionManager>();
        return builder;
    }

    /// <summary>
    /// Registers EF Core as the outbox store provider.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEfCoreOutboxStore(this TurboMediatorBuilder builder)
    {
        builder.Services.TryAddScoped<IOutboxStore, EfCoreOutboxStore>();
        return builder;
    }

    /// <summary>
    /// Registers EF Core as the audit store provider.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEfCoreAuditStore(this TurboMediatorBuilder builder)
    {
        builder.Services.TryAddScoped<IAuditStore, EfCoreAuditStore>();
        return builder;
    }

    /// <summary>
    /// Registers EF Core as the inbox store provider.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder UseEfCoreInboxStore(this TurboMediatorBuilder builder)
    {
        builder.Services.TryAddScoped<IInboxStore, EfCoreInboxStore>();
        return builder;
    }

    /// <summary>
    /// Configures all Entity Framework behaviors at once (convenience method).
    /// Registers EF Core provider + transaction, outbox, and audit behaviors.
    /// </summary>
    /// <param name="builder">The TurboMediator builder.</param>
    /// <param name="configureTransaction">Optional configuration for transaction options.</param>
    /// <param name="configureOutbox">Optional configuration for outbox options.</param>
    /// <param name="configureAudit">Optional configuration for audit options.</param>
    /// <returns>The builder for chaining.</returns>
    public static TurboMediatorBuilder WithEntityFramework(
        this TurboMediatorBuilder builder,
        Action<TransactionOptions>? configureTransaction = null,
        Action<OutboxOptions>? configureOutbox = null,
        Action<AuditOptions>? configureAudit = null)
    {
        return builder
            .UseEntityFramework()
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
    /// <param name="outboxBuilder">The outbox builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static OutboxBuilder UseEfCoreStore(this OutboxBuilder outboxBuilder)
    {
        outboxBuilder.Services.TryAddScoped<IOutboxStore, EfCoreOutboxStore>();
        return outboxBuilder;
    }

    /// <summary>
    /// Uses the Entity Framework Core inbox store for idempotency.
    /// </summary>
    /// <param name="outboxBuilder">The outbox builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static OutboxBuilder UseEfCoreInboxStore(this OutboxBuilder outboxBuilder)
    {
        outboxBuilder.Services.TryAddScoped<IInboxStore, EfCoreInboxStore>();
        return outboxBuilder;
    }
}
