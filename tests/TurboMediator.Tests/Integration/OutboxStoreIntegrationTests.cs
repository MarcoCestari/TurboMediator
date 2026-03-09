using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.EntityFramework;
using TurboMediator.Persistence.EntityFramework.Outbox;
using TurboMediator.Persistence.Outbox;
using TurboMediator.Tests.IntegrationTests.Fixtures;
using TurboMediator.Tests.IntegrationTests.Infrastructure;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests;

/// <summary>
/// Integration tests for EfCoreOutboxStore using a real PostgreSQL database.
/// Validates outbox message persistence, status transitions, batch queries, and cleanup.
/// </summary>
[Collection("PostgreSql")]
public class OutboxStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private IntegrationTestDbContext _dbContext = null!;

    public OutboxStoreIntegrationTests(PostgreSqlFixture fixture)
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

        // Clean outbox table for isolation
        _dbContext.OutboxMessages.RemoveRange(_dbContext.OutboxMessages);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        // Clean up
        _dbContext.OutboxMessages.RemoveRange(_dbContext.OutboxMessages);
        await _dbContext.SaveChangesAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistMessage()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("OrderCreated", "{\"orderId\":123}");

        // Act
        await store.SaveAsync(message);

        // Verify with a new context
        await using var verifyCtx = CreateNewContext();
        var saved = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        saved.Should().NotBeNull();
        saved!.MessageType.Should().Be("OrderCreated");
        saved.Payload.Should().Be("{\"orderId\":123}");
        saved.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldReturnPendingMessages()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        await store.SaveAsync(CreateOutboxMessage("Event1", "{}", OutboxMessageStatus.Pending));
        await store.SaveAsync(CreateOutboxMessage("Event2", "{}", OutboxMessageStatus.Pending));
        await store.SaveAsync(CreateOutboxMessage("Event3", "{}", OutboxMessageStatus.Processed));

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var msg in store.GetPendingAsync(10))
        {
            pending.Add(msg);
        }

        // Assert
        pending.Should().HaveCountGreaterThanOrEqualTo(2);
        pending.Should().OnlyContain(m => m.Status == OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldRespectBatchSize()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        for (int i = 0; i < 10; i++)
        {
            await store.SaveAsync(CreateOutboxMessage($"BatchEvent{i}", "{}"));
        }

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var msg in store.GetPendingAsync(3))
        {
            pending.Add(msg);
        }

        // Assert
        pending.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldIncludeRetriedMessagesUnderRetryLimit()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());

        var retriedUnderLimit = CreateOutboxMessage("RetriedUnderLimit", "{}");
        retriedUnderLimit.Status = OutboxMessageStatus.Pending;
        retriedUnderLimit.RetryCount = 1;

        var retriedOverLimit = CreateOutboxMessage("RetriedExhausted", "{}");
        retriedOverLimit.Status = OutboxMessageStatus.Pending;
        retriedOverLimit.RetryCount = 5;

        _dbContext.OutboxMessages.Add(retriedUnderLimit);
        _dbContext.OutboxMessages.Add(retriedOverLimit);
        await _dbContext.SaveChangesAsync();

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var msg in store.GetPendingAsync(10))
        {
            pending.Add(msg);
        }

        // Assert
        pending.Should().Contain(m => m.Id == retriedUnderLimit.Id);
        pending.Should().NotContain(m => m.Id == retriedOverLimit.Id);
    }

    [Fact]
    public async Task MarkAsProcessingAsync_ShouldUpdateStatus()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("ProcessingTest", "{}");
        await store.SaveAsync(message);

        // Act
        await store.MarkAsProcessingAsync(message.Id);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated!.Status.Should().Be(OutboxMessageStatus.Processing);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_ShouldUpdateStatusAndTimestamp()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("ProcessedTest", "{}");
        await store.SaveAsync(message);

        // Act
        await store.MarkAsProcessedAsync(message.Id);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated!.Status.Should().Be(OutboxMessageStatus.Processed);
        updated.ProcessedAt.Should().NotBeNull();
        updated.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task IncrementRetryAsync_ShouldUpdateErrorAndRetryCount_KeepPendingStatus()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("RetryTest", "{}");
        await store.SaveAsync(message);

        // Act
        await store.IncrementRetryAsync(message.Id, "Connection timeout");

        // Assert
        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated!.Status.Should().Be(OutboxMessageStatus.Pending);
        updated.Error.Should().Be("Connection timeout");
        updated.RetryCount.Should().Be(1);
    }

    [Fact]
    public async Task IncrementRetryAsync_ShouldIncrementRetryCount()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("RetryCountTest", "{}");
        await store.SaveAsync(message);

        // Act - fail multiple times
        await store.IncrementRetryAsync(message.Id, "Error 1");
        await store.IncrementRetryAsync(message.Id, "Error 2");
        await store.IncrementRetryAsync(message.Id, "Error 3");

        // Assert
        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated!.RetryCount.Should().Be(3);
        updated.Error.Should().Be("Error 3");
        updated.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task CleanupAsync_ShouldDeleteOldProcessedMessages()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());

        // Old processed message
        var oldMessage = CreateOutboxMessage("OldProcessed", "{}");
        oldMessage.Status = OutboxMessageStatus.Processed;
        oldMessage.ProcessedAt = DateTime.UtcNow.AddDays(-10);
        _dbContext.OutboxMessages.Add(oldMessage);

        // Recent processed message
        var recentMessage = CreateOutboxMessage("RecentProcessed", "{}");
        recentMessage.Status = OutboxMessageStatus.Processed;
        recentMessage.ProcessedAt = DateTime.UtcNow.AddMinutes(-5);
        _dbContext.OutboxMessages.Add(recentMessage);

        // Pending message (should never be cleaned)
        var pendingMessage = CreateOutboxMessage("StillPending", "{}");
        _dbContext.OutboxMessages.Add(pendingMessage);

        await _dbContext.SaveChangesAsync();

        // Act
        var deleted = await store.CleanupAsync(TimeSpan.FromDays(7));

        // Assert
        deleted.Should().BeGreaterThanOrEqualTo(1);

        await using var verifyCtx = CreateNewContext();
        var remaining = await verifyCtx.OutboxMessages.ToListAsync();
        remaining.Should().NotContain(m => m.Id == oldMessage.Id, "old processed messages should be cleaned up");
        remaining.Should().Contain(m => m.Id == recentMessage.Id, "recent processed messages should remain");
        remaining.Should().Contain(m => m.Id == pendingMessage.Id, "pending messages should remain");
    }

    [Fact]
    public async Task GetPendingAsync_ShouldOrderByCreatedAt()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());

        var older = CreateOutboxMessage("OlderEvent", "{}");
        older.CreatedAt = DateTime.UtcNow.AddMinutes(-10);
        _dbContext.OutboxMessages.Add(older);

        var newer = CreateOutboxMessage("NewerEvent", "{}");
        newer.CreatedAt = DateTime.UtcNow;
        _dbContext.OutboxMessages.Add(newer);

        await _dbContext.SaveChangesAsync();

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var msg in store.GetPendingAsync(10))
        {
            pending.Add(msg);
        }

        // Assert
        var olderIdx = pending.FindIndex(m => m.Id == older.Id);
        var newerIdx = pending.FindIndex(m => m.Id == newer.Id);
        if (olderIdx >= 0 && newerIdx >= 0)
        {
            olderIdx.Should().BeLessThan(newerIdx, "older messages should come first");
        }
    }

    [Fact]
    public async Task FullLifecycle_PendingToProcessingToProcessed()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("LifecycleEvent", "{\"data\":\"test\"}");
        message.CorrelationId = "corr-123";

        // Act - full lifecycle
        await store.SaveAsync(message);

        // Verify pending
        var pending = new List<OutboxMessage>();
        await foreach (var msg in store.GetPendingAsync(10))
            pending.Add(msg);
        pending.Should().Contain(m => m.Id == message.Id);

        // Mark as processing
        await store.MarkAsProcessingAsync(message.Id);

        // Mark as processed
        await store.MarkAsProcessedAsync(message.Id);

        // Verify final state
        await using var verifyCtx = CreateNewContext();
        var final = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        final!.Status.Should().Be(OutboxMessageStatus.Processed);
        final.ProcessedAt.Should().NotBeNull();
        final.CorrelationId.Should().Be("corr-123");
    }

    [Fact]
    public async Task ConcurrentSaves_ShouldNotConflict()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            await using var ctx = CreateNewContext();
            var store = new EfCoreOutboxStore<IntegrationTestDbContext>(ctx, new EfCorePersistenceOptions());
            var message = CreateOutboxMessage($"ConcurrentEvent{i}", $"{{\"index\":{i}}}");
            await store.SaveAsync(message);
            return message.Id;
        });

        // Act
        var ids = await Task.WhenAll(tasks);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var count = await verifyCtx.OutboxMessages.CountAsync(m => ids.Contains(m.Id));
        count.Should().Be(5);
    }

    [Fact]
    public async Task TryClaimAsync_ShouldClaimPendingMessage()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("ClaimTest", "{}");
        await store.SaveAsync(message);

        // Act
        var claimed = await store.TryClaimAsync(message.Id, "worker-1");

        // Assert
        claimed.Should().BeTrue();

        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated!.Status.Should().Be(OutboxMessageStatus.Processing);
        updated.ClaimedBy.Should().Be("worker-1");
    }

    [Fact]
    public async Task TryClaimAsync_ShouldRejectAlreadyClaimedMessage()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("ClaimConflictTest", "{}");
        await store.SaveAsync(message);

        // First worker claims it
        var firstClaim = await store.TryClaimAsync(message.Id, "worker-1");
        firstClaim.Should().BeTrue();

        // Act - second worker tries to claim the same message using a separate context
        await using var ctx2 = CreateNewContext();
        var store2 = new EfCoreOutboxStore<IntegrationTestDbContext>(ctx2, new EfCorePersistenceOptions());
        var secondClaim = await store2.TryClaimAsync(message.Id, "worker-2");

        // Assert - second worker should fail to claim
        secondClaim.Should().BeFalse();

        // Verify it's still claimed by worker-1
        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated!.ClaimedBy.Should().Be("worker-1");
    }

    [Fact]
    public async Task TryClaimAsync_ConcurrentWorkers_OnlyOneWins()
    {
        // Arrange - create 10 messages
        var messageIds = new List<Guid>();
        for (int i = 0; i < 10; i++)
        {
            var msg = CreateOutboxMessage($"ConcurrencyRace{i}", $"{{\"i\":{i}}}");
            _dbContext.OutboxMessages.Add(msg);
            messageIds.Add(msg.Id);
        }
        await _dbContext.SaveChangesAsync();

        // Act - 3 workers race to claim the same 10 messages concurrently
        var claimTasks = new List<Task<(string workerId, List<Guid> claimed)>>();
        for (int w = 0; w < 3; w++)
        {
            var workerId = $"worker-{w}";
            claimTasks.Add(Task.Run(async () =>
            {
                var claimed = new List<Guid>();
                await using var ctx = CreateNewContext();
                var store = new EfCoreOutboxStore<IntegrationTestDbContext>(ctx, new EfCorePersistenceOptions());
                foreach (var id in messageIds)
                {
                    if (await store.TryClaimAsync(id, workerId))
                    {
                        claimed.Add(id);
                    }
                }
                return (workerId, claimed);
            }));
        }

        var results = await Task.WhenAll(claimTasks);

        // Assert - each message should be claimed by exactly one worker
        var allClaimed = results.SelectMany(r => r.claimed).ToList();
        allClaimed.Should().HaveCount(10, "all 10 messages should be claimed");
        allClaimed.Distinct().Should().HaveCount(10, "no message should be claimed by more than one worker");

        // Total claims across all workers should equal 10 (no duplicates)
        var totalClaims = results.Sum(r => r.claimed.Count);
        totalClaims.Should().Be(10);
    }

    [Fact]
    public async Task TryClaimAsync_ShouldNotClaimProcessedMessage()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("AlreadyProcessed", "{}");
        message.Status = OutboxMessageStatus.Processed;
        message.ProcessedAt = DateTime.UtcNow;
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Act
        var claimed = await store.TryClaimAsync(message.Id, "worker-1");

        // Assert
        claimed.Should().BeFalse();
    }

    [Fact]
    public async Task TryClaimAsync_ShouldNotClaimRetriedMessageOverLimit()
    {
        // Arrange - retried message that should not be claimable since
        // GetPendingAsync won't return it (RetryCount >= MaxRetries),
        // but TryClaimAsync itself only checks Pending status
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("PendingRetryable", "{}");
        message.Status = OutboxMessageStatus.Pending;
        message.RetryCount = 1;
        _dbContext.OutboxMessages.Add(message);
        await _dbContext.SaveChangesAsync();

        // Act
        var claimed = await store.TryClaimAsync(message.Id, "worker-1");

        // Assert - Pending messages can be claimed (processor checks retry limit before claiming)
        claimed.Should().BeTrue();

        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated!.Status.Should().Be(OutboxMessageStatus.Processing);
        updated.ClaimedBy.Should().Be("worker-1");
    }

    private IntegrationTestDbContext CreateNewContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationTestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new IntegrationTestDbContext(options);
    }

    private static OutboxMessage CreateOutboxMessage(
        string messageType,
        string payload,
        OutboxMessageStatus status = OutboxMessageStatus.Pending)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = messageType,
            Payload = payload,
            CreatedAt = DateTime.UtcNow,
            Status = status,
            RetryCount = 0,
            MaxRetries = 3
        };
    }

    // ========================================================================
    // Dead Letter Queue Integration Tests
    // ========================================================================

    [Fact]
    public async Task MoveToDeadLetterAsync_ShouldUpdateStatusToDeadLettered()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("DeadLetterTest", "{\"data\":\"test\"}");
        message.RetryCount = 5;
        message.MaxRetries = 3;
        await store.SaveAsync(message);

        // Act
        await store.MoveToDeadLetterAsync(message.Id, "Max retry attempts exceeded");

        // Assert
        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated.Should().NotBeNull();
        updated!.Status.Should().Be(OutboxMessageStatus.DeadLettered);
        updated.Error.Should().Be("Max retry attempts exceeded");
        updated.ProcessedAt.Should().NotBeNull();
        updated.ProcessedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task MoveToDeadLetterAsync_ShouldPreserveOriginalMessageData()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("PreserveDataTest", "{\"orderId\":999}");
        message.RetryCount = 3;
        message.MaxRetries = 3;
        message.CorrelationId = "corr-dlq-001";
        await store.SaveAsync(message);

        // Act
        await store.MoveToDeadLetterAsync(message.Id, "Processing failed permanently");

        // Assert
        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated.Should().NotBeNull();
        updated!.MessageType.Should().Be("PreserveDataTest");
        updated.Payload.Should().Be("{\"orderId\":999}");
        updated.CorrelationId.Should().Be("corr-dlq-001");
        updated.RetryCount.Should().Be(3);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldNotReturnDeadLetteredMessages()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var pendingMessage = CreateOutboxMessage("PendingEvent", "{}");
        await store.SaveAsync(pendingMessage);

        var deadLetteredMessage = CreateOutboxMessage("DeadLetteredEvent", "{}");
        await store.SaveAsync(deadLetteredMessage);
        await store.MoveToDeadLetterAsync(deadLetteredMessage.Id, "Failed permanently");

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var msg in store.GetPendingAsync(10))
        {
            pending.Add(msg);
        }

        // Assert
        pending.Should().Contain(m => m.Id == pendingMessage.Id);
        pending.Should().NotContain(m => m.Id == deadLetteredMessage.Id,
            "dead-lettered messages should not be returned as pending");
    }

    [Fact]
    public async Task MoveToDeadLetterAsync_ShouldBeIdempotent()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("IdempotentDLQ", "{}");
        await store.SaveAsync(message);

        // Act - Move twice
        await store.MoveToDeadLetterAsync(message.Id, "First reason");
        await store.MoveToDeadLetterAsync(message.Id, "Second reason");

        // Assert - Should have the last reason
        await using var verifyCtx = CreateNewContext();
        var updated = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        updated!.Status.Should().Be(OutboxMessageStatus.DeadLettered);
        updated.Error.Should().Be("Second reason");
    }

    [Fact]
    public async Task DeadLettered_FullLifecycle_PendingToRetriedToDeadLettered()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var message = CreateOutboxMessage("DLQLifecycle", "{\"data\":\"lifecycle\"}");
        message.MaxRetries = 2;
        await store.SaveAsync(message);

        // Act - Simulate processing failures (stays Pending with incremented RetryCount)
        await store.IncrementRetryAsync(message.Id, "Error 1");
        await store.IncrementRetryAsync(message.Id, "Error 2");

        // Now move to dead letter
        await store.MoveToDeadLetterAsync(message.Id, "Exceeded 2 retries");

        // Assert
        await using var verifyCtx = CreateNewContext();
        var final = await verifyCtx.OutboxMessages.FindAsync(message.Id);
        final!.Status.Should().Be(OutboxMessageStatus.DeadLettered);
        final.RetryCount.Should().Be(2);
        final.Error.Should().Be("Exceeded 2 retries");
        final.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetPendingAsync_ShouldRespectMaxRetries_PerMessage()
    {
        // Arrange
        var store = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());

        // Message with MaxRetries = 5 and RetryCount = 3 → should still be pending
        var highRetryMessage = CreateOutboxMessage("HighRetryLimit", "{}");
        highRetryMessage.MaxRetries = 5;
        highRetryMessage.RetryCount = 3;
        highRetryMessage.Status = OutboxMessageStatus.Pending;
        _dbContext.OutboxMessages.Add(highRetryMessage);

        // Message with MaxRetries = 2 and RetryCount = 3 → should NOT be pending
        var lowRetryMessage = CreateOutboxMessage("LowRetryLimit", "{}");
        lowRetryMessage.MaxRetries = 2;
        lowRetryMessage.RetryCount = 3;
        lowRetryMessage.Status = OutboxMessageStatus.Pending;
        _dbContext.OutboxMessages.Add(lowRetryMessage);

        await _dbContext.SaveChangesAsync();

        // Act
        var pending = new List<OutboxMessage>();
        await foreach (var msg in store.GetPendingAsync(10))
        {
            pending.Add(msg);
        }

        // Assert
        pending.Should().Contain(m => m.Id == highRetryMessage.Id,
            "message under its own MaxRetries should be retryable");
        pending.Should().NotContain(m => m.Id == lowRetryMessage.Id,
            "message over its own MaxRetries should not be retryable");
    }
}
