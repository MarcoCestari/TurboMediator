using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.EF.Inbox;
using TurboMediator.Persistence.Inbox;
using TurboMediator.Tests.IntegrationTests.Fixtures;
using TurboMediator.Tests.IntegrationTests.Infrastructure;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests;

/// <summary>
/// Integration tests for EfCoreInboxStore using a real PostgreSQL database.
/// Validates inbox message deduplication, persistence, and cleanup.
/// </summary>
[Collection("PostgreSql")]
public class InboxStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private IntegrationTestDbContext _dbContext = null!;

    public InboxStoreIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<IntegrationTestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        _dbContext = new IntegrationTestDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();

        // Clean inbox table for isolation
        _dbContext.InboxMessages.RemoveRange(_dbContext.InboxMessages);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _dbContext.InboxMessages.RemoveRange(_dbContext.InboxMessages);
        await _dbContext.SaveChangesAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task RecordAsync_ShouldPersistInboxMessage()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);
        var message = CreateInboxMessage("msg-001", "OrderHandler");

        // Act
        await store.RecordAsync(message);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var saved = await verifyCtx.InboxMessages
            .FirstOrDefaultAsync(m => m.MessageId == "msg-001" && m.HandlerType == "OrderHandler");
        saved.Should().NotBeNull();
        saved!.MessageType.Should().Be("TestMessage");
        saved.ReceivedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        saved.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_ShouldReturnFalse_WhenNotRecorded()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);

        // Act
        var result = await store.HasBeenProcessedAsync("nonexistent-msg", "SomeHandler");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_ShouldReturnTrue_WhenRecorded()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);
        var message = CreateInboxMessage("msg-002", "PaymentHandler");
        await store.RecordAsync(message);

        // Act
        var result = await store.HasBeenProcessedAsync("msg-002", "PaymentHandler");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task HasBeenProcessedAsync_ShouldReturnFalse_ForDifferentHandler()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);
        var message = CreateInboxMessage("msg-003", "HandlerA");
        await store.RecordAsync(message);

        // Act - Same message ID but different handler type
        var result = await store.HasBeenProcessedAsync("msg-003", "HandlerB");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RecordAsync_ShouldNotThrow_WhenDuplicateInserted()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);
        var message1 = CreateInboxMessage("msg-004", "DuplicateHandler");
        await store.RecordAsync(message1);

        var message2 = CreateInboxMessage("msg-004", "DuplicateHandler");

        // Act - Should not throw
        var act = () => store.RecordAsync(message2).AsTask();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RecordAsync_ShouldAllowSameMessageId_ForDifferentHandlers()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);

        // Act
        await store.RecordAsync(CreateInboxMessage("msg-005", "Handler1"));
        await store.RecordAsync(CreateInboxMessage("msg-005", "Handler2"));

        // Assert
        await using var verifyCtx = CreateNewContext();
        var count = await verifyCtx.InboxMessages
            .CountAsync(m => m.MessageId == "msg-005");
        count.Should().Be(2);
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteOldProcessedMessages()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);

        // Old processed message
        var oldMessage = CreateInboxMessage("old-msg", "CleanupHandler");
        oldMessage.ProcessedAt = DateTime.UtcNow.AddDays(-10);
        await store.RecordAsync(oldMessage);

        // Recent processed message
        var recentMessage = CreateInboxMessage("recent-msg", "CleanupHandler");
        recentMessage.ProcessedAt = DateTime.UtcNow.AddMinutes(-5);
        await store.RecordAsync(recentMessage);

        // Act
        var deleted = await store.CleanupAsync(TimeSpan.FromDays(7));

        // Assert
        deleted.Should().BeGreaterThanOrEqualTo(1);

        await using var verifyCtx = CreateNewContext();
        var remaining = await verifyCtx.InboxMessages.ToListAsync();
        remaining.Should().NotContain(m => m.MessageId == "old-msg", "old messages should be cleaned up");
        remaining.Should().Contain(m => m.MessageId == "recent-msg", "recent messages should remain");
    }

    [Fact]
    public async Task CleanupAsync_ShouldNotDeleteUnprocessedMessages()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);

        var unprocessedMessage = new InboxMessage
        {
            MessageId = "unprocessed-msg",
            HandlerType = "CleanupHandler",
            MessageType = "TestMessage",
            ReceivedAt = DateTime.UtcNow.AddDays(-10),
            ProcessedAt = null
        };
        _dbContext.InboxMessages.Add(unprocessedMessage);
        await _dbContext.SaveChangesAsync();

        // Act
        var deleted = await store.CleanupAsync(TimeSpan.FromDays(7));

        // Assert
        deleted.Should().Be(0);

        await using var verifyCtx = CreateNewContext();
        var exists = await verifyCtx.InboxMessages
            .AnyAsync(m => m.MessageId == "unprocessed-msg");
        exists.Should().BeTrue("unprocessed messages should not be deleted");
    }

    [Fact]
    public async Task FullLifecycle_CheckThenRecordThenVerify()
    {
        // Arrange
        var store = new EfCoreInboxStore(_dbContext);
        var messageId = "lifecycle-msg";
        var handlerType = "LifecycleHandler";

        // Act & Assert - Step 1: Not yet processed
        var isProcessed = await store.HasBeenProcessedAsync(messageId, handlerType);
        isProcessed.Should().BeFalse();

        // Act & Assert - Step 2: Record processing
        await store.RecordAsync(CreateInboxMessage(messageId, handlerType));

        // Act & Assert - Step 3: Now it's processed
        isProcessed = await store.HasBeenProcessedAsync(messageId, handlerType);
        isProcessed.Should().BeTrue();
    }

    [Fact]
    public async Task ConcurrentRecords_ShouldNotConflict()
    {
        // Arrange - Multiple workers recording different messages concurrently
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            await using var ctx = CreateNewContext();
            var store = new EfCoreInboxStore(ctx);
            var message = CreateInboxMessage($"concurrent-msg-{i}", "ConcurrentHandler");
            await store.RecordAsync(message);
        });

        // Act
        await Task.WhenAll(tasks);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var count = await verifyCtx.InboxMessages
            .CountAsync(m => m.HandlerType == "ConcurrentHandler");
        count.Should().Be(5);
    }

    [Fact]
    public async Task HasBeenProcessedAsync_WithNewContext_ShouldReflectPersistedState()
    {
        // Arrange - Record using one context
        var store = new EfCoreInboxStore(_dbContext);
        await store.RecordAsync(CreateInboxMessage("cross-ctx-msg", "CrossCtxHandler"));

        // Act - Check using a different context (simulates different service instance)
        await using var otherCtx = CreateNewContext();
        var otherStore = new EfCoreInboxStore(otherCtx);
        var result = await otherStore.HasBeenProcessedAsync("cross-ctx-msg", "CrossCtxHandler");

        // Assert
        result.Should().BeTrue("persisted state should be visible across contexts");
    }

    private IntegrationTestDbContext CreateNewContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationTestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new IntegrationTestDbContext(options);
    }

    private static InboxMessage CreateInboxMessage(string messageId, string handlerType)
    {
        return new InboxMessage
        {
            MessageId = messageId,
            HandlerType = handlerType,
            MessageType = "TestMessage",
            ReceivedAt = DateTime.UtcNow,
            ProcessedAt = DateTime.UtcNow
        };
    }
}
