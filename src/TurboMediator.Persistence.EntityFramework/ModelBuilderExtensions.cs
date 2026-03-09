using Microsoft.EntityFrameworkCore;

namespace TurboMediator.Persistence.EntityFramework;

/// <summary>
/// Extension methods for configuring persistence entity configurations in DbContext.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Applies all persistence entity configurations (Inbox, Outbox, Audit) to the model builder.
    /// Call this in your DbContext's <see cref="DbContext.OnModelCreating"/> method.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplyPersistenceConfiguration(
        this ModelBuilder modelBuilder,
        EfCorePersistenceOptions? options = null)
    {
        modelBuilder.ApplyConfiguration(new InboxMessageEntityConfiguration(options));
        modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration(options));
        modelBuilder.ApplyConfiguration(new AuditEntryEntityConfiguration(options));
        return modelBuilder;
    }

    /// <summary>
    /// Applies only the inbox message entity configuration.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplyInboxConfiguration(
        this ModelBuilder modelBuilder,
        EfCorePersistenceOptions? options = null)
    {
        modelBuilder.ApplyConfiguration(new InboxMessageEntityConfiguration(options));
        return modelBuilder;
    }

    /// <summary>
    /// Applies only the outbox message entity configuration.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplyOutboxConfiguration(
        this ModelBuilder modelBuilder,
        EfCorePersistenceOptions? options = null)
    {
        modelBuilder.ApplyConfiguration(new OutboxMessageEntityConfiguration(options));
        return modelBuilder;
    }

    /// <summary>
    /// Applies only the audit entry entity configuration.
    /// </summary>
    /// <param name="modelBuilder">The model builder.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The model builder for chaining.</returns>
    public static ModelBuilder ApplyAuditConfiguration(
        this ModelBuilder modelBuilder,
        EfCorePersistenceOptions? options = null)
    {
        modelBuilder.ApplyConfiguration(new AuditEntryEntityConfiguration(options));
        return modelBuilder;
    }
}
