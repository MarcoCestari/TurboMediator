using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TurboMediator.Persistence.Audit;
using TurboMediator.Persistence.EntityFramework;
using TurboMediator.Persistence.EntityFramework.Audit;
using TurboMediator.Persistence.EntityFramework.Outbox;
using TurboMediator.Persistence.EntityFramework.Transaction;
using TurboMediator.Persistence.Outbox;
using TurboMediator.Persistence.Transaction;
using TurboMediator.Tests.IntegrationTests.Fixtures;
using TurboMediator.Tests.IntegrationTests.Infrastructure;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests;

/// <summary>
/// End-to-end integration tests combining Transaction, Outbox, and Audit behaviors
/// running against a real PostgreSQL database.
/// </summary>
[Collection("PostgreSql")]
public class FullPipelineIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private IntegrationTestDbContext _dbContext = null!;

    public FullPipelineIntegrationTests(PostgreSqlFixture fixture)
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

        // Clean all tables
        _dbContext.Products.RemoveRange(_dbContext.Products);
        _dbContext.OutboxMessages.RemoveRange(_dbContext.OutboxMessages);
        _dbContext.AuditEntries.RemoveRange(_dbContext.AuditEntries);
        await _dbContext.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _dbContext.Products.RemoveRange(_dbContext.Products);
        _dbContext.OutboxMessages.RemoveRange(_dbContext.OutboxMessages);
        _dbContext.AuditEntries.RemoveRange(_dbContext.AuditEntries);
        await _dbContext.SaveChangesAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task TransactionWithAudit_ShouldPersistBothOnSuccess()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var auditStore = new EfCoreAuditStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());

        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var txBehavior = new TransactionBehavior<CreateProductWithAuditCommand, TestProduct>(
            transactionManager, transactionOptions);

        var command = new CreateProductWithAuditCommand("Keyboard", 150m);

        // Act
        var result = await txBehavior.Handle(
            command,
            async (msg, ct) => {
                var product = new TestProduct { Name = "Keyboard", Price = 150m, Stock = 50 };
                _dbContext.Products.Add(product);

                // Also write audit within the same transaction
                var audit = new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Action = "CreateProduct",
                    EntityType = "Product",
                    EntityId = "new",
                    UserId = "integration-test",
                    Timestamp = DateTime.UtcNow,
                    Success = true,
                    DurationMs = 10
                };
                _dbContext.AuditEntries.Add(audit);

                return product;
            },
            CancellationToken.None);

        // Assert
        await using var verifyCtx = CreateNewContext();
        var savedProduct = await verifyCtx.Products.FirstOrDefaultAsync(p => p.Name == "Keyboard");
        savedProduct.Should().NotBeNull();

        var savedAudit = await verifyCtx.AuditEntries.FirstOrDefaultAsync(a => a.Action == "CreateProduct");
        savedAudit.Should().NotBeNull();
    }

    [Fact]
    public async Task TransactionWithAudit_ShouldRollbackBothOnFailure()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var txBehavior = new TransactionBehavior<CreateProductWithAuditCommand, TestProduct>(
            transactionManager, transactionOptions);

        var command = new CreateProductWithAuditCommand("FailedProduct", 100m);

        // Act
        var act = async () => await txBehavior.Handle(
            command,
            async (msg, ct) => {
                _dbContext.Products.Add(new TestProduct { Name = "FailedProduct", Price = 100m });
                _dbContext.AuditEntries.Add(new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Action = "FailedCreate",
                    EntityType = "Product",
                    Timestamp = DateTime.UtcNow,
                    Success = true
                });
                await _dbContext.SaveChangesAsync();

                throw new InvalidOperationException("Simulated pipeline failure");
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert - both product and audit should be rolled back
        await using var verifyCtx = CreateNewContext();
        var product = await verifyCtx.Products.FirstOrDefaultAsync(p => p.Name == "FailedProduct");
        product.Should().BeNull("transaction was rolled back");

        var audit = await verifyCtx.AuditEntries.FirstOrDefaultAsync(a => a.Action == "FailedCreate");
        audit.Should().BeNull("audit should also be rolled back within the same transaction");
    }

    [Fact]
    public async Task TransactionWithOutbox_ShouldPersistBothAtomically()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var txBehavior = new TransactionBehavior<CreateProductWithOutboxCommand, TestProduct>(
            transactionManager, transactionOptions);

        var command = new CreateProductWithOutboxCommand("Monitor", 800m);

        // Act
        var result = await txBehavior.Handle(
            command,
            async (msg, ct) => {
                var product = new TestProduct { Name = "Monitor", Price = 800m, Stock = 20 };
                _dbContext.Products.Add(product);

                // Write outbox message within the same transaction
                _dbContext.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = "ProductCreated",
                    Payload = "{\"name\":\"Monitor\",\"price\":800}",
                    CreatedAt = DateTime.UtcNow,
                    Status = OutboxMessageStatus.Pending
                });

                return product;
            },
            CancellationToken.None);

        // Assert - both product and outbox message should exist
        await using var verifyCtx = CreateNewContext();
        var savedProduct = await verifyCtx.Products.FirstOrDefaultAsync(p => p.Name == "Monitor");
        savedProduct.Should().NotBeNull();

        var outboxMsg = await verifyCtx.OutboxMessages.FirstOrDefaultAsync(m => m.MessageType == "ProductCreated");
        outboxMsg.Should().NotBeNull();
        outboxMsg!.Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task TransactionWithOutbox_ShouldRollbackBothOnFailure()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var txBehavior = new TransactionBehavior<CreateProductWithOutboxCommand, TestProduct>(
            transactionManager, transactionOptions);

        var command = new CreateProductWithOutboxCommand("FailedMonitor", 800m);

        // Act
        var act = async () => await txBehavior.Handle(
            command,
            async (msg, ct) => {
                _dbContext.Products.Add(new TestProduct { Name = "FailedMonitor", Price = 800m });
                _dbContext.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = "FailedProductCreated",
                    Payload = "{}",
                    CreatedAt = DateTime.UtcNow,
                    Status = OutboxMessageStatus.Pending
                });
                await _dbContext.SaveChangesAsync();
                throw new InvalidOperationException("Outbox pipeline failure");
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert
        await using var verifyCtx = CreateNewContext();
        var product = await verifyCtx.Products.FirstOrDefaultAsync(p => p.Name == "FailedMonitor");
        product.Should().BeNull();

        var outbox = await verifyCtx.OutboxMessages.FirstOrDefaultAsync(m => m.MessageType == "FailedProductCreated");
        outbox.Should().BeNull("outbox message should also be rolled back");
    }

    [Fact]
    public async Task OutboxProcessor_ShouldProcessPendingMessages()
    {
        // Arrange - seed pending outbox messages
        var outboxStore = new EfCoreOutboxStore<IntegrationTestDbContext>(_dbContext, new EfCorePersistenceOptions());
        var msg1 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "ProcessorTest1",
            Payload = "{\"test\":1}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };
        var msg2 = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            MessageType = "ProcessorTest2",
            Payload = "{\"test\":2}",
            CreatedAt = DateTime.UtcNow,
            Status = OutboxMessageStatus.Pending
        };

        await outboxStore.SaveAsync(msg1);
        await outboxStore.SaveAsync(msg2);

        // Create a proper scoped setup for the processor
        var services = new ServiceCollection();
        services.AddDbContext<IntegrationTestDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped<IOutboxStore, EfCoreOutboxStore<IntegrationTestDbContext>>(sp =>
            new EfCoreOutboxStore<IntegrationTestDbContext>(sp.GetRequiredService<IntegrationTestDbContext>(), new EfCorePersistenceOptions()));
        services.AddLogging(b => b.AddDebug());

        await using var sp = services.BuildServiceProvider();

        var processorOptions = new OutboxProcessorOptions
        {
            ProcessingInterval = TimeSpan.FromMilliseconds(100),
            BatchSize = 10,
            MaxRetryAttempts = 3,
            PublishToMessageBroker = false,
            EnableAutoCleanup = false
        };

        var logger = sp.GetRequiredService<ILogger<OutboxProcessor>>();
        var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
        var processor = new OutboxProcessor(scopeFactory, processorOptions, logger);

        // Act - start processor and wait briefly
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await processor.StartAsync(cts.Token);
            await Task.Delay(1000); // Give processor time to run
            await processor.StopAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) { }

        // Assert
        await using var verifyCtx = CreateNewContext();
        var processed1 = await verifyCtx.OutboxMessages.FindAsync(msg1.Id);
        processed1!.Status.Should().Be(OutboxMessageStatus.Processed);

        var processed2 = await verifyCtx.OutboxMessages.FindAsync(msg2.Id);
        processed2!.Status.Should().Be(OutboxMessageStatus.Processed);
    }

    [Fact]
    public async Task FullPipeline_TransactionAuditOutbox_AllPersisted()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var txBehavior = new TransactionBehavior<CreateProductFullCommand, TestProduct>(
            transactionManager, transactionOptions);

        var command = new CreateProductFullCommand("GPU", 1500m, "admin");

        // Act
        var result = await txBehavior.Handle(
            command,
            async (msg, ct) => {
                // Create product
                var product = new TestProduct { Name = "GPU", Price = 1500m, Stock = 5 };
                _dbContext.Products.Add(product);

                // Create outbox message
                _dbContext.OutboxMessages.Add(new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    MessageType = "ProductCreated",
                    Payload = "{\"name\":\"GPU\",\"price\":1500}",
                    CreatedAt = DateTime.UtcNow,
                    Status = OutboxMessageStatus.Pending
                });

                // Create audit entry
                _dbContext.AuditEntries.Add(new AuditEntry
                {
                    Id = Guid.NewGuid(),
                    Action = "CreateProduct",
                    EntityType = "Product",
                    EntityId = "gpu-new",
                    UserId = "admin",
                    Timestamp = DateTime.UtcNow,
                    Success = true,
                    RequestPayload = "{\"name\":\"GPU\",\"price\":1500}",
                    DurationMs = 15
                });

                return product;
            },
            CancellationToken.None);

        // Assert - all three should be persisted atomically
        await using var verifyCtx = CreateNewContext();

        var product = await verifyCtx.Products.FirstOrDefaultAsync(p => p.Name == "GPU");
        product.Should().NotBeNull("product should be persisted");

        var outbox = await verifyCtx.OutboxMessages
            .FirstOrDefaultAsync(m => m.Payload.Contains("GPU"));
        outbox.Should().NotBeNull("outbox message should be persisted");

        var audit = await verifyCtx.AuditEntries
            .FirstOrDefaultAsync(a => a.EntityId == "gpu-new");
        audit.Should().NotBeNull("audit entry should be persisted");
        audit!.UserId.Should().Be("admin");
    }

    private IntegrationTestDbContext CreateNewContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationTestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new IntegrationTestDbContext(options);
    }

    // Test messages
    public record CreateProductWithAuditCommand(string Name, decimal Price) : ICommand<TestProduct>;
    public record CreateProductWithOutboxCommand(string Name, decimal Price) : ICommand<TestProduct>;
    public record CreateProductFullCommand(string Name, decimal Price, string UserId) : ICommand<TestProduct>;
}
