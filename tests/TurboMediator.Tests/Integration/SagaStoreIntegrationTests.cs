using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Saga;
using TurboMediator.Saga.EntityFramework;
using TurboMediator.Tests.IntegrationTests.Fixtures;
using TurboMediator.Tests.IntegrationTests.Infrastructure;
using Xunit;

namespace TurboMediator.Tests.IntegrationTests;

/// <summary>
/// Integration tests for EfCoreSagaStore using a real PostgreSQL database.
/// Validates saga state persistence, status queries, and lifecycle management.
/// </summary>
[Collection("PostgreSql")]
public class SagaStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private ServiceProvider _serviceProvider = null!;
    private IntegrationTestDbContext _context = null!;
    private ISagaStore _store = null!;

    public SagaStoreIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var sagaOptions = new EfCoreSagaStoreOptions
        {
            TableName = "SagaStates",
            EnableOptimisticConcurrency = true,
            AutoMigrate = false
        };

        var services = new ServiceCollection();
        services.AddDbContext<IntegrationTestDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString));
        services.AddSingleton(sagaOptions);
        services.AddScoped<ISagaStore, EfCoreSagaStore<IntegrationTestDbContext>>();

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<IntegrationTestDbContext>();
        _store = _serviceProvider.GetRequiredService<ISagaStore>();

        await _context.Database.EnsureCreatedAsync();

        // Clean saga table for isolation
        _context.SagaStates.RemoveRange(_context.SagaStates);
        await _context.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _context.SagaStates.RemoveRange(_context.SagaStates);
        await _context.SaveChangesAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task SaveAsync_ShouldCreateNewSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "OrderSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0,
            Data = "{\"orderId\":123,\"amount\":99.90}",
            CorrelationId = "order-corr-123"
        };

        // Act
        await _store.SaveAsync(state);

        // Assert - verify with a new scope
        await using var verifyScope = CreateScope();
        var store = verifyScope.ServiceProvider.GetRequiredService<ISagaStore>();
        var retrieved = await store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.SagaId.Should().Be(sagaId);
        retrieved.SagaType.Should().Be("OrderSaga");
        retrieved.Status.Should().Be(SagaStatus.Running);
        retrieved.Data.Should().Be("{\"orderId\":123,\"amount\":99.90}");
        retrieved.CorrelationId.Should().Be("order-corr-123");
    }

    [Fact]
    public async Task SaveAsync_ShouldUpdateExistingSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "PaymentSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0,
            Data = "{\"step\":\"initial\"}"
        };
        await _store.SaveAsync(state);

        // Act
        state.Status = SagaStatus.Completed;
        state.CurrentStep = 3;
        state.CompletedAt = DateTime.UtcNow;
        state.Data = "{\"step\":\"completed\",\"result\":\"success\"}";
        await _store.SaveAsync(state);

        // Assert
        await using var verifyScope = CreateScope();
        var store = verifyScope.ServiceProvider.GetRequiredService<ISagaStore>();
        var retrieved = await store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(SagaStatus.Completed);
        retrieved.CurrentStep.Should().Be(3);
        retrieved.CompletedAt.Should().NotBeNull();
        retrieved.Data.Should().Contain("completed");
    }

    [Fact]
    public async Task GetAsync_ShouldReturnNullWhenNotFound()
    {
        // Act
        var result = await _store.GetAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingAsync_ShouldReturnRunningAndCompensatingSagas()
    {
        // Arrange
        var runningId = Guid.NewGuid();
        var compensatingId = Guid.NewGuid();
        var completedId = Guid.NewGuid();
        var failedId = Guid.NewGuid();

        await _store.SaveAsync(new SagaState
        {
            SagaId = runningId, SagaType = "PendingTestSaga",
            Status = SagaStatus.Running
        });
        await _store.SaveAsync(new SagaState
        {
            SagaId = compensatingId, SagaType = "PendingTestSaga",
            Status = SagaStatus.Compensating
        });
        await _store.SaveAsync(new SagaState
        {
            SagaId = completedId, SagaType = "PendingTestSaga",
            Status = SagaStatus.Completed
        });
        await _store.SaveAsync(new SagaState
        {
            SagaId = failedId, SagaType = "PendingTestSaga",
            Status = SagaStatus.Failed
        });

        // Act
        var pending = new List<SagaState>();
        await foreach (var saga in _store.GetPendingAsync("PendingTestSaga"))
        {
            pending.Add(saga);
        }

        // Assert
        pending.Should().HaveCount(2);
        pending.Should().Contain(s => s.SagaId == runningId);
        pending.Should().Contain(s => s.SagaId == compensatingId);
        pending.Should().NotContain(s => s.SagaId == completedId);
        pending.Should().NotContain(s => s.SagaId == failedId);
    }

    [Fact]
    public async Task GetPendingAsync_ShouldFilterBySagaType()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        await _store.SaveAsync(new SagaState
        {
            SagaId = orderId, SagaType = "FilterOrderSaga",
            Status = SagaStatus.Running
        });
        await _store.SaveAsync(new SagaState
        {
            SagaId = paymentId, SagaType = "FilterPaymentSaga",
            Status = SagaStatus.Running
        });

        // Act
        var orderSagas = new List<SagaState>();
        await foreach (var saga in _store.GetPendingAsync("FilterOrderSaga"))
        {
            orderSagas.Add(saga);
        }

        // Assert
        orderSagas.Should().HaveCount(1);
        orderSagas.Should().OnlyContain(s => s.SagaType == "FilterOrderSaga");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        await _store.SaveAsync(new SagaState
        {
            SagaId = sagaId, SagaType = "DeleteTest",
            Status = SagaStatus.Failed
        });

        // Act
        await _store.DeleteAsync(sagaId);

        // Assert
        var retrieved = await _store.GetAsync(sagaId);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_ShouldNotThrowWhenNotFound()
    {
        // Act & Assert
        var act = async () => await _store.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SagaLifecycle_CompleteFLow_Running_To_Completed()
    {
        // Arrange
        var sagaId = Guid.NewGuid();

        // Step 1: Create saga
        await _store.SaveAsync(new SagaState
        {
            SagaId = sagaId,
            SagaType = "CheckoutSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0,
            Data = "{\"items\":[{\"id\":1,\"qty\":2}]}"
        });

        // Step 2: Advance to step 1
        var state = await _store.GetAsync(sagaId);
        state!.CurrentStep = 1;
        state.Data = "{\"items\":[{\"id\":1,\"qty\":2}],\"stockReserved\":true}";
        await _store.SaveAsync(state);

        // Step 3: Advance to step 2
        state = await _store.GetAsync(sagaId);
        state!.CurrentStep = 2;
        state.Data = "{\"items\":[{\"id\":1,\"qty\":2}],\"stockReserved\":true,\"paymentId\":\"PAY-123\"}";
        await _store.SaveAsync(state);

        // Step 4: Complete
        state = await _store.GetAsync(sagaId);
        state!.Status = SagaStatus.Completed;
        state.CurrentStep = 3;
        state.CompletedAt = DateTime.UtcNow;
        await _store.SaveAsync(state);

        // Assert final state
        await using var verifyScope = CreateScope();
        var store = verifyScope.ServiceProvider.GetRequiredService<ISagaStore>();
        var final = await store.GetAsync(sagaId);
        final.Should().NotBeNull();
        final!.Status.Should().Be(SagaStatus.Completed);
        final.CurrentStep.Should().Be(3);
        final.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SagaLifecycle_CompensationFlow()
    {
        // Arrange
        var sagaId = Guid.NewGuid();

        // Step 1: Create saga
        await _store.SaveAsync(new SagaState
        {
            SagaId = sagaId,
            SagaType = "FailingSaga",
            Status = SagaStatus.Running,
            CurrentStep = 2,
            Data = "{\"step1\":\"done\",\"step2\":\"done\"}"
        });

        // Step 2: Fail at step 3, start compensation
        var state = await _store.GetAsync(sagaId);
        state!.Status = SagaStatus.Compensating;
        state.Error = "Payment declined";
        await _store.SaveAsync(state);

        // Step 3: Compensation completes, mark as failed
        state = await _store.GetAsync(sagaId);
        state!.Status = SagaStatus.Failed;
        state.CompletedAt = DateTime.UtcNow;
        await _store.SaveAsync(state);

        // Assert
        await using var verifyScope = CreateScope();
        var store = verifyScope.ServiceProvider.GetRequiredService<ISagaStore>();
        var final = await store.GetAsync(sagaId);
        final!.Status.Should().Be(SagaStatus.Failed);
        final.Error.Should().Be("Payment declined");
        final.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MultipleSagaConcurrency_ShouldHandleParallelCreation()
    {
        // Arrange & Act
        var tasks = Enumerable.Range(0, 10).Select(async i =>
        {
            await using var scope = CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<ISagaStore>();
            var sagaId = Guid.NewGuid();
            await store.SaveAsync(new SagaState
            {
                SagaId = sagaId,
                SagaType = $"ConcurrentSaga{i % 3}",
                Status = SagaStatus.Running,
                CurrentStep = 0,
                Data = $"{{\"index\":{i}}}"
            });
            return sagaId;
        });

        var sagaIds = await Task.WhenAll(tasks);

        // Assert
        await using var verifyScope = CreateScope();
        var verifyStore = verifyScope.ServiceProvider.GetRequiredService<ISagaStore>();
        foreach (var sagaId in sagaIds)
        {
            var saga = await verifyStore.GetAsync(sagaId);
            saga.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task AutoMigrate_ShouldCreateTablesAutomatically()
    {
        // Arrange - use AutoMigrate option with existing context
        var sagaOptions = new EfCoreSagaStoreOptions
        {
            TableName = "SagaStates",
            AutoMigrate = true,
            EnableOptimisticConcurrency = false
        };

        var services = new ServiceCollection();
        services.AddDbContext<IntegrationTestDbContext>(options =>
            options.UseNpgsql(_fixture.ConnectionString));
        services.AddSingleton(sagaOptions);
        services.AddScoped<ISagaStore, EfCoreSagaStore<IntegrationTestDbContext>>();

        await using var sp = services.BuildServiceProvider();
        var store = sp.GetRequiredService<ISagaStore>();

        // Act - should work with auto-migrate (tables already exist from EnsureCreated)
        var sagaId = Guid.NewGuid();
        await store.SaveAsync(new SagaState
        {
            SagaId = sagaId,
            SagaType = "AutoMigrateTest",
            Status = SagaStatus.Running,
            CurrentStep = 0
        });

        // Assert
        var retrieved = await store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.SagaType.Should().Be("AutoMigrateTest");
    }

    [Fact]
    public async Task SagaWithLargeData_ShouldPersistCorrectly()
    {
        // Arrange
        var largeData = new string('x', 50_000); // 50KB payload
        var sagaId = Guid.NewGuid();

        // Act
        await _store.SaveAsync(new SagaState
        {
            SagaId = sagaId,
            SagaType = "LargeDataSaga",
            Status = SagaStatus.Running,
            Data = $"{{\"payload\":\"{largeData}\"}}"
        });

        // Assert
        await using var verifyScope = CreateScope();
        var store = verifyScope.ServiceProvider.GetRequiredService<ISagaStore>();
        var retrieved = await store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.Data.Should().Contain(largeData);
    }

    private AsyncServiceScope CreateScope()
    {
        return _serviceProvider.CreateAsyncScope();
    }
}
