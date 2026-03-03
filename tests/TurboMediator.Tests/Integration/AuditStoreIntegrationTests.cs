using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.EF.Audit;
using TurboMediator.Tests.IntegrationTests.Fixtures;
using TurboMediator.Tests.IntegrationTests.Infrastructure;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests;

/// <summary>
/// Integration tests for EfCoreAuditStore using a real PostgreSQL database.
/// Validates audit entry persistence with real database queries including filtering and time ranges.
/// </summary>
[Collection("PostgreSql")]
public class AuditStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private IntegrationTestDbContext _dbContext = null!;

    public AuditStoreIntegrationTests(PostgreSqlFixture fixture)
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

        // Clean audit table for isolation
        _dbContext.AuditEntries.RemoveRange(_dbContext.AuditEntries);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _dbContext.AuditEntries.RemoveRange(_dbContext.AuditEntries);
        await _dbContext.SaveChangesAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistAuditEntry()
    {
        // Arrange
        var store = new EfCoreAuditStore(_dbContext);
        var entry = CreateAuditEntry("CreateOrder", "Order", "ORD-001", "user-1");

        // Act
        await store.SaveAsync(entry);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var saved = await verifyCtx.AuditEntries.FindAsync(entry.Id);
        saved.Should().NotBeNull();
        saved!.Action.Should().Be("CreateOrder");
        saved.EntityType.Should().Be("Order");
        saved.EntityId.Should().Be("ORD-001");
        saved.UserId.Should().Be("user-1");
        saved.Success.Should().BeTrue();
    }

    [Fact]
    public async Task GetByEntityAsync_ShouldReturnMatchingEntries()
    {
        // Arrange
        var store = new EfCoreAuditStore(_dbContext);
        await store.SaveAsync(CreateAuditEntry("Create", "Order", "ORD-100", "user-1"));
        await store.SaveAsync(CreateAuditEntry("Update", "Order", "ORD-100", "user-2"));
        await store.SaveAsync(CreateAuditEntry("Create", "Order", "ORD-200", "user-1"));
        await store.SaveAsync(CreateAuditEntry("Create", "Product", "PRD-100", "user-1"));

        // Act
        var entries = new List<AuditEntry>();
        await foreach (var entry in store.GetByEntityAsync("Order", "ORD-100"))
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.EntityType == "Order" && e.EntityId == "ORD-100");
    }

    [Fact]
    public async Task GetByEntityAsync_ShouldReturnOrderedByTimestampDescending()
    {
        // Arrange
        var store = new EfCoreAuditStore(_dbContext);
        var older = CreateAuditEntry("Create", "Inv", "INV-1", "user-1");
        older.Timestamp = DateTime.UtcNow.AddHours(-2);
        await store.SaveAsync(older);

        var newer = CreateAuditEntry("Update", "Inv", "INV-1", "user-1");
        newer.Timestamp = DateTime.UtcNow;
        await store.SaveAsync(newer);

        // Act
        var entries = new List<AuditEntry>();
        await foreach (var entry in store.GetByEntityAsync("Inv", "INV-1"))
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().HaveCount(2);
        entries[0].Timestamp.Should().BeOnOrAfter(entries[1].Timestamp);
    }

    [Fact]
    public async Task GetByUserAsync_ShouldReturnUserEntries()
    {
        // Arrange
        var store = new EfCoreAuditStore(_dbContext);
        await store.SaveAsync(CreateAuditEntry("Action1", "Entity", "E1", "admin"));
        await store.SaveAsync(CreateAuditEntry("Action2", "Entity", "E2", "admin"));
        await store.SaveAsync(CreateAuditEntry("Action3", "Entity", "E3", "other-user"));

        // Act
        var entries = new List<AuditEntry>();
        await foreach (var entry in store.GetByUserAsync("admin"))
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().HaveCount(2);
        entries.Should().OnlyContain(e => e.UserId == "admin");
    }

    [Fact]
    public async Task GetByTimeRangeAsync_ShouldFilterByRange()
    {
        // Arrange
        var store = new EfCoreAuditStore(_dbContext);

        var now = DateTime.UtcNow;
        var e1 = CreateAuditEntry("Old", "E", "1", "u");
        e1.Timestamp = now.AddDays(-5);
        await store.SaveAsync(e1);

        var e2 = CreateAuditEntry("InRange", "E", "2", "u");
        e2.Timestamp = now.AddHours(-1);
        await store.SaveAsync(e2);

        var e3 = CreateAuditEntry("Recent", "E", "3", "u");
        e3.Timestamp = now;
        await store.SaveAsync(e3);

        // Act
        var entries = new List<AuditEntry>();
        await foreach (var entry in store.GetByTimeRangeAsync(now.AddDays(-1), now.AddMinutes(1)))
        {
            entries.Add(entry);
        }

        // Assert
        entries.Should().HaveCountGreaterThanOrEqualTo(2);
        entries.Should().Contain(e => e.Id == e2.Id);
        entries.Should().Contain(e => e.Id == e3.Id);
        entries.Should().NotContain(e => e.Id == e1.Id, "it's outside the time range");
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistFailureDetails()
    {
        // Arrange
        var store = new EfCoreAuditStore(_dbContext);
        var entry = CreateAuditEntry("FailedAction", "Order", "ORD-ERR", "user-1");
        entry.Success = false;
        entry.ErrorMessage = "Validation failed: invalid email";
        entry.DurationMs = 42;

        // Act
        await store.SaveAsync(entry);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var saved = await verifyCtx.AuditEntries.FindAsync(entry.Id);
        saved!.Success.Should().BeFalse();
        saved.ErrorMessage.Should().Be("Validation failed: invalid email");
        saved.DurationMs.Should().Be(42);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistMetadata()
    {
        // Arrange
        var store = new EfCoreAuditStore(_dbContext);
        var entry = CreateAuditEntry("MetaAction", "E", "1", "u");
        entry.RequestPayload = "{\"name\":\"test\"}";
        entry.ResponsePayload = "{\"id\":1}";
        entry.IpAddress = "192.168.1.100";
        entry.UserAgent = "TestAgent/1.0";
        entry.CorrelationId = "corr-456";
        entry.Metadata = "{\"source\":\"integration-test\"}";

        // Act
        await store.SaveAsync(entry);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var saved = await verifyCtx.AuditEntries.FindAsync(entry.Id);
        saved!.RequestPayload.Should().Be("{\"name\":\"test\"}");
        saved.ResponsePayload.Should().Be("{\"id\":1}");
        saved.IpAddress.Should().Be("192.168.1.100");
        saved.UserAgent.Should().Be("TestAgent/1.0");
        saved.CorrelationId.Should().Be("corr-456");
        saved.Metadata.Should().Be("{\"source\":\"integration-test\"}");
    }

    [Fact]
    public async Task ConcurrentAuditWrites_ShouldNotConflict()
    {
        // Arrange & Act
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await using var ctx = CreateNewContext();
            var store = new EfCoreAuditStore(ctx);
            var entry = CreateAuditEntry($"ConcurrentAction{i}", "E", $"id-{i}", $"user-{i}");
            await store.SaveAsync(entry);
            return entry.Id;
        });

        var ids = await Task.WhenAll(tasks);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var count = await verifyCtx.AuditEntries.CountAsync(e => ids.Contains(e.Id));
        count.Should().Be(10);
    }

    private IntegrationTestDbContext CreateNewContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationTestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new IntegrationTestDbContext(options);
    }

    private static AuditEntry CreateAuditEntry(string action, string entityType, string entityId, string userId)
    {
        return new AuditEntry
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            Success = true,
            DurationMs = 10
        };
    }
}
