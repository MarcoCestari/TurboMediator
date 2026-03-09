using System.Data;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Persistence.EntityFramework.Transaction;
using TurboMediator.Persistence.Transaction;
using TurboMediator.Tests.IntegrationTests.Fixtures;
using TurboMediator.Tests.IntegrationTests.Infrastructure;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests;

/// <summary>
/// Integration tests for EfCoreTransactionManager using a real PostgreSQL database.
/// Validates actual transaction commit/rollback behavior that cannot be tested with InMemory provider.
/// </summary>
[Collection("PostgreSql")]
public class TransactionIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private IntegrationTestDbContext _dbContext = null!;
    private readonly string _schema;

    public TransactionIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _schema = $"tx_{Guid.NewGuid():N}".Substring(0, 20);
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<IntegrationTestDbContext>()
            .UseNpgsql(_fixture.ConnectionString, o => o.MigrationsHistoryTable("__EFMigrationsHistory", _schema))
            .Options;

        _dbContext = new IntegrationTestDbContext(options);
        await _dbContext.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.Database.EnsureDeletedAsync();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Transaction_ShouldCommitOnSuccess()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var behavior = new TransactionBehavior<CreateProductCommand, TestProduct>(transactionManager, transactionOptions);

        var command = new CreateProductCommand("Laptop", 2999.99m);
        var product = new TestProduct { Name = "Laptop", Price = 2999.99m, Stock = 10 };

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => {
                _dbContext.Products.Add(product);
                return product;
            },
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Laptop");

        // Verify with a new context to ensure data is truly persisted
        await using var verifyContext = CreateNewContext();
        var saved = await verifyContext.Products.FirstOrDefaultAsync(p => p.Name == "Laptop");
        saved.Should().NotBeNull();
        saved!.Price.Should().Be(2999.99m);
    }

    [Fact]
    public async Task Transaction_ShouldRollbackOnException()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var behavior = new TransactionBehavior<CreateProductCommand, TestProduct>(transactionManager, transactionOptions);

        var command = new CreateProductCommand("Defective", 100m);

        // Act
        var act = async () => await behavior.Handle(
            command,
            async (msg, ct) => {
                _dbContext.Products.Add(new TestProduct { Name = "Defective", Price = 100m });
                throw new InvalidOperationException("Simulated failure");
            },
            CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();

        // Verify rollback - the product should NOT exist
        await using var verifyContext = CreateNewContext();
        var products = await verifyContext.Products.Where(p => p.Name == "Defective").ToListAsync();
        products.Should().BeEmpty("transaction should have been rolled back");
    }

    [Fact]
    public async Task Transaction_ShouldSupportReadCommittedIsolation()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions
        {
            IsolationLevel = IsolationLevel.ReadCommitted,
            AutoSaveChanges = true
        };
        var behavior = new TransactionBehavior<CreateProductCommand, TestProduct>(transactionManager, transactionOptions);

        var command = new CreateProductCommand("IsolationTest", 50m);
        var product = new TestProduct { Name = "IsolationTest", Price = 50m, Stock = 5 };

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => {
                _dbContext.Products.Add(product);
                return product;
            },
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await using var verifyContext = CreateNewContext();
        var saved = await verifyContext.Products.FirstOrDefaultAsync(p => p.Name == "IsolationTest");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_ShouldSupportSerializableIsolation()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions
        {
            IsolationLevel = IsolationLevel.Serializable,
            AutoSaveChanges = true
        };
        var behavior = new TransactionBehavior<CreateProductCommand, TestProduct>(transactionManager, transactionOptions);

        var command = new CreateProductCommand("SerializableTest", 75m);
        var product = new TestProduct { Name = "SerializableTest", Price = 75m };

        // Act
        var result = await behavior.Handle(
            command,
            async (msg, ct) => {
                _dbContext.Products.Add(product);
                return product;
            },
            CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        await using var verifyContext = CreateNewContext();
        var saved = await verifyContext.Products.FirstOrDefaultAsync(p => p.Name == "SerializableTest");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_WithAutoSaveDisabled_ShouldNotPersistWithoutExplicitSave()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = false };
        var behavior = new TransactionBehavior<CreateProductCommand, TestProduct>(transactionManager, transactionOptions);

        var command = new CreateProductCommand("NoAutoSave", 25m);
        var product = new TestProduct { Name = "NoAutoSave", Price = 25m };

        // Act - handler adds entity but auto save is off, and handler doesn't call SaveChanges
        var result = await behavior.Handle(
            command,
            async (msg, ct) => {
                _dbContext.Products.Add(product);
                return product;
            },
            CancellationToken.None);

        // Assert - entity should NOT be persisted because auto save was disabled
        await using var verifyContext = CreateNewContext();
        var saved = await verifyContext.Products.FirstOrDefaultAsync(p => p.Name == "NoAutoSave");
        saved.Should().BeNull("auto save was disabled and handler did not explicitly call SaveChanges");
    }

    [Fact]
    public async Task Transaction_MultipleOperations_ShouldBeAtomic()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);
        var transactionOptions = new TransactionOptions { AutoSaveChanges = true };
        var behavior = new TransactionBehavior<CreateProductCommand, TestProduct>(transactionManager, transactionOptions);

        var command = new CreateProductCommand("AtomicTest", 100m);

        // Act - multiple operations in a single transaction that fails after first insert
        var act = async () => await behavior.Handle(
            command,
            async (msg, ct) => {
                _dbContext.Products.Add(new TestProduct { Name = "AtomicFirst", Price = 10m });
                _dbContext.Products.Add(new TestProduct { Name = "AtomicSecond", Price = 20m });
                await _dbContext.SaveChangesAsync();

                // Force failure after partial work
                throw new InvalidOperationException("Atomic failure");
            },
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();

        // Assert - neither product should exist due to transaction rollback
        await using var verifyContext = CreateNewContext();
        var products = await verifyContext.Products
            .Where(p => p.Name.StartsWith("Atomic"))
            .ToListAsync();
        products.Should().BeEmpty("all operations should be rolled back atomically");
    }

    [Fact]
    public async Task Transaction_ExecuteWithStrategy_ShouldWork()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);

        // Act
        var result = await transactionManager.ExecuteWithStrategyAsync<TestProduct>(async ct =>
        {
            var transaction = await transactionManager.BeginTransactionAsync(cancellationToken: ct);
            try
            {
                _dbContext.Products.Add(new TestProduct { Name = "StrategyTest", Price = 999m, Stock = 1 });
                await _dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                return new TestProduct { Name = "StrategyTest", Price = 999m };
            }
            catch
            {
                await transaction.RollbackAsync(ct);
                throw;
            }
            finally
            {
                await transaction.DisposeAsync();
            }
        }, CancellationToken.None);

        // Assert
        result.Name.Should().Be("StrategyTest");
        await using var verifyContext = CreateNewContext();
        var saved = await verifyContext.Products.FirstOrDefaultAsync(p => p.Name == "StrategyTest");
        saved.Should().NotBeNull();
    }

    [Fact]
    public async Task Transaction_HasActiveTransaction_ShouldReflectState()
    {
        // Arrange
        var transactionManager = new EfCoreTransactionManager<IntegrationTestDbContext>(_dbContext);

        // Assert - no active transaction initially
        transactionManager.HasActiveTransaction.Should().BeFalse();

        // Act
        var tx = await transactionManager.BeginTransactionAsync();
        transactionManager.HasActiveTransaction.Should().BeTrue();

        await tx.CommitAsync();
        await tx.DisposeAsync();

        // After dispose, no active transaction
        transactionManager.HasActiveTransaction.Should().BeFalse();
    }

    private IntegrationTestDbContext CreateNewContext()
    {
        var options = new DbContextOptionsBuilder<IntegrationTestDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new IntegrationTestDbContext(options);
    }

    // Test messages
    public record CreateProductCommand(string Name, decimal Price) : ICommand<TestProduct>;
}
