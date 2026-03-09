using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Persistence.EntityFramework;
using TurboMediator.Persistence.EntityFramework.Audit;
using TurboMediator.Persistence.EntityFramework.Outbox;
using TurboMediator.Persistence.EntityFramework.Transaction;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.Outbox;
using TurboMediator.Persistence.Transaction;
using Xunit;

namespace TurboMediator.Tests.EntityFramework;

/// <summary>
/// Tests for Entity Framework behaviors.
/// </summary>
public class EntityFrameworkBehaviorTests
{
    // ==================== Transaction Tests ====================

    [Fact]
    public async Task TransactionBehavior_ShouldCommitOnSuccess()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new TestDbContext(options);
        var transactionManager = new EfCoreTransactionManager<TestDbContext>(dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var behavior = new TransactionBehavior<CreateEntityCommand, TestEntity>(transactionManager, transactionOptions);

        var command = new CreateEntityCommand("Test Entity");
        var expectedEntity = new TestEntity { Id = 1, Name = "Test Entity" };

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => {
                dbContext.Entities.Add(expectedEntity);
                return expectedEntity;
            },
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Test Entity");

        // Verify entity was saved
        var savedEntity = await dbContext.Entities.FindAsync(1);
        savedEntity.Should().NotBeNull();
    }

    [Fact]
    public async Task TransactionBehavior_ShouldRollbackOnException()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var dbContext = new TestDbContext(options);
        var transactionManager = new EfCoreTransactionManager<TestDbContext>(dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = false };
        var behavior = new TransactionBehavior<CreateEntityCommand, TestEntity>(transactionManager, transactionOptions);

        var command = new CreateEntityCommand("Test Entity");

        // Act & Assert
        var act = async () => await behavior.Handle(
            command,
            async (msg, ct) => {
                dbContext.Entities.Add(new TestEntity { Id = 1, Name = "Test Entity" });
                throw new InvalidOperationException("Test exception");
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void TransactionalAttribute_ShouldHaveDefaultValues()
    {
        // Act
        var attribute = new TransactionalAttribute();

        // Assert
        attribute.IsolationLevel.Should().Be(System.Data.IsolationLevel.ReadCommitted);
        attribute.TimeoutSeconds.Should().Be(30);
        attribute.AutoSaveChanges.Should().BeTrue();
    }

    [Fact]
    public void TransactionalAttribute_ShouldAcceptIsolationLevel()
    {
        // Act
        var attribute = new TransactionalAttribute(System.Data.IsolationLevel.Serializable);

        // Assert
        attribute.IsolationLevel.Should().Be(System.Data.IsolationLevel.Serializable);
    }

    // ==================== Audit Tests ====================

    [Fact]
    public async Task AuditBehavior_ShouldCreateAuditEntry()
    {
        // Arrange
        var auditStore = new InMemoryAuditStore();
        var auditOptions = new AuditOptions
        {
            IncludeRequest = true,
            IncludeResponse = true,
            UserIdProvider = () => "test-user"
        };
        var behavior = new AuditBehavior<AuditedCommand, string>(auditStore, auditOptions);

        var command = new AuditedCommand("test-id", "Test Data");

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => "Success",
            CancellationToken.None);

        // Assert
        result.Should().Be("Success");
        auditStore.Entries.Should().HaveCount(1);

        var entry = auditStore.Entries[0];
        entry.Action.Should().Be("AuditedCommand");
        entry.UserId.Should().Be("test-user");
        entry.Success.Should().BeTrue();
        entry.EntityId.Should().Be("test-id");
    }

    [Fact]
    public async Task AuditBehavior_ShouldRecordFailure()
    {
        // Arrange
        var auditStore = new InMemoryAuditStore();
        var auditOptions = new AuditOptions { AuditFailures = true };
        var behavior = new AuditBehavior<AuditedCommand, string>(auditStore, auditOptions);

        var command = new AuditedCommand("test-id", "Test Data");

        // Act & Assert
        var act = async () => await behavior.Handle(
            command,
            (msg, ct) => throw new InvalidOperationException("Test error"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        auditStore.Entries.Should().HaveCount(1);
        var entry = auditStore.Entries[0];
        entry.Success.Should().BeFalse();
        entry.ErrorMessage.Should().Be("Test error");
    }

    [Fact]
    public void AuditableAttribute_ShouldHaveDefaultValues()
    {
        // Act
        var attribute = new AuditableAttribute();

        // Assert
        attribute.IncludeRequest.Should().BeTrue();
        attribute.IncludeResponse.Should().BeFalse();
        attribute.ActionName.Should().BeNull();
    }

    [Fact]
    public async Task AuditBehavior_ShouldExcludeSensitiveProperties()
    {
        // Arrange
        var auditStore = new InMemoryAuditStore();
        var auditOptions = new AuditOptions
        {
            IncludeRequest = true,
            GlobalExcludeProperties = new[] { "Password", "Secret" }
        };
        var behavior = new AuditBehavior<SensitiveCommand, string>(auditStore, auditOptions);

        var command = new SensitiveCommand("user123", "my-secret-password");

        // Act
        await behavior.Handle(
            command,
            async (msg, ct) => "OK",
            CancellationToken.None);

        // Assert
        var entry = auditStore.Entries[0];
        entry.RequestPayload.Should().NotContain("my-secret-password");
        entry.RequestPayload.Should().Contain("user123");
    }

    // ==================== Outbox Tests ====================

    [Fact]
    public async Task OutboxStore_ShouldSaveMessage()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new OutboxTestDbContext(options);
        var outboxStore = new EfCoreOutboxStore<OutboxTestDbContext>(dbContext, new EfCorePersistenceOptions());

        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "TestEvent",
            Payload = "{\"data\":\"test\"}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };

        // Act
        await outboxStore.SaveAsync(message);

        // Assert
        var saved = await dbContext.OutboxMessages.FindAsync(message.Id);
        saved.Should().NotBeNull();
        saved!.MessageType.Should().Be("TestEvent");
    }

    [Fact]
    public async Task OutboxStore_ShouldMarkAsProcessed()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new OutboxTestDbContext(options);
        var outboxStore = new EfCoreOutboxStore<OutboxTestDbContext>(dbContext, new EfCorePersistenceOptions());

        var messageId = Guid.NewGuid();
        var message = new OutboxMessage
        {
            Id = messageId,
            MessageType = "TestEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };

        await dbContext.OutboxMessages.AddAsync(message);
        await dbContext.SaveChangesAsync();

        // Act
        await outboxStore.MarkAsProcessedAsync(messageId);

        // Assert
        var updated = await dbContext.OutboxMessages.FindAsync(messageId);
        updated!.Status.Should().Be(OutboxMessageStatus.Processed);
        updated.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task OutboxStore_ShouldIncrementRetry()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<OutboxTestDbContext>()
            .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
            .Options;

        using var dbContext = new OutboxTestDbContext(options);
        var outboxStore = new EfCoreOutboxStore<OutboxTestDbContext>(dbContext, new EfCorePersistenceOptions());

        var messageId = Guid.NewGuid();
        var message = new OutboxMessage
        {
            Id = messageId,
            MessageType = "TestEvent",
            Payload = "{}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending,
            RetryCount = 0
        };

        await dbContext.OutboxMessages.AddAsync(message);
        await dbContext.SaveChangesAsync();

        // Act
        await outboxStore.IncrementRetryAsync(messageId, "Connection failed");

        // Assert - status stays Pending, retry count incremented, error recorded
        var updated = await dbContext.OutboxMessages.FindAsync(messageId);
        updated!.Status.Should().Be(OutboxMessageStatus.Pending);
        updated.Error.Should().Be("Connection failed");
        updated.RetryCount.Should().Be(1);
    }

    [Fact]
    public void WithOutboxAttribute_ShouldHaveDefaultValues()
    {
        // Act
        var attribute = new WithOutboxAttribute();

        // Assert
        attribute.PublishImmediately.Should().BeFalse();
        attribute.MaxRetries.Should().Be(3);
    }

    // ==================== Test DbContexts ====================

    public class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        public DbSet<TestEntity> Entities => Set<TestEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestEntity>().HasKey(e => e.Id);
        }
    }

    public class OutboxTestDbContext : DbContext
    {
        public OutboxTestDbContext(DbContextOptions<OutboxTestDbContext> options) : base(options) { }

        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<OutboxMessage>().HasKey(e => e.Id);
        }
    }

    public class TestEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    // ==================== Test Messages ====================

    public record CreateEntityCommand(string Name) : ICommand<TestEntity>;

    [Auditable]
    public record AuditedCommand(string Id, string Data) : ICommand<string>;

    [Auditable(IncludeRequest = true)]
    public record SensitiveCommand(string UserId, string Password) : ICommand<string>;

    // ==================== Test Helpers ====================

    public class InMemoryAuditStore : IAuditStore
    {
        public List<AuditEntry> Entries { get; } = new();

        public ValueTask SaveAsync(AuditEntry entry, CancellationToken cancellationToken = default)
        {
            Entries.Add(entry);
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<AuditEntry> GetByEntityAsync(string entityType, string entityId, CancellationToken cancellationToken = default)
            => Entries.Where(e => e.EntityType == entityType && e.EntityId == entityId).ToAsyncEnumerable();

        public IAsyncEnumerable<AuditEntry> GetByUserAsync(string userId, CancellationToken cancellationToken = default)
            => Entries.Where(e => e.UserId == userId).ToAsyncEnumerable();

        public IAsyncEnumerable<AuditEntry> GetByTimeRangeAsync(DateTime from, DateTime to, CancellationToken cancellationToken = default)
            => Entries.Where(e => e.Timestamp >= from && e.Timestamp <= to).ToAsyncEnumerable();
    }
}
