using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.Inbox;
using TurboMediator.Persistence.Outbox;
using TurboMediator.Saga.EntityFramework;

namespace TurboMediator.Tests.IntegrationTests.Infrastructure;

/// <summary>
/// Unified DbContext for all integration tests (Transaction, Outbox, Audit, Saga).
/// All tables are created together to avoid EnsureCreatedAsync conflicts when multiple
/// test classes share the same PostgreSQL database.
/// </summary>
public class IntegrationTestDbContext : DbContext
{
    private readonly EfCoreSagaStoreOptions? _sagaOptions;

    public IntegrationTestDbContext(DbContextOptions<IntegrationTestDbContext> options)
        : base(options) { }

    public IntegrationTestDbContext(
        DbContextOptions<IntegrationTestDbContext> options,
        EfCoreSagaStoreOptions? sagaOptions)
        : base(options)
    {
        _sagaOptions = sagaOptions;
    }

    public DbSet<TestProduct> Products => Set<TestProduct>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<InboxMessage> InboxMessages => Set<InboxMessage>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SagaStateEntity> SagaStates => Set<SagaStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TestProduct>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Price).HasPrecision(18, 2);
        });

        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MessageType).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Payload).IsRequired();
            entity.Property(e => e.Error).HasMaxLength(4000);
            entity.Property(e => e.CorrelationId).HasMaxLength(100);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.Ignore(e => e.Headers);
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).IsRequired().HasMaxLength(200);
            entity.Property(e => e.EntityType).HasMaxLength(200);
            entity.Property(e => e.EntityId).HasMaxLength(200);
            entity.Property(e => e.UserId).HasMaxLength(200);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.EntityType, e.EntityId });
            entity.HasIndex(e => e.UserId);
        });

        modelBuilder.Entity<InboxMessage>(entity =>
        {
            entity.HasKey(e => new { e.MessageId, e.HandlerType });
            entity.Property(e => e.MessageId).IsRequired().HasMaxLength(500);
            entity.Property(e => e.HandlerType).IsRequired().HasMaxLength(500);
            entity.Property(e => e.MessageType).HasMaxLength(500);
            entity.HasIndex(e => e.ProcessedAt);
        });

        // Saga state configuration
        var sagaOpts = _sagaOptions ?? new EfCoreSagaStoreOptions
        {
            TableName = "SagaStates",
            EnableOptimisticConcurrency = true
        };
        modelBuilder.ApplySagaStateConfiguration(sagaOpts);
    }
}

/// <summary>
/// Simple entity for transaction tests.
/// </summary>
public class TestProduct
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
