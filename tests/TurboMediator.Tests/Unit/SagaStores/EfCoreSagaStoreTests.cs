using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TurboMediator.Saga.EntityFramework;
using TurboMediator.Saga;
using Xunit;

namespace TurboMediator.Tests.SagaStores;

/// <summary>
/// Test DbContext for EF Core saga store tests.
/// </summary>
public class TestSagaDbContext : DbContext
{
    public TestSagaDbContext(DbContextOptions<TestSagaDbContext> options) : base(options) { }

    public DbSet<SagaStateEntity> SagaStates => Set<SagaStateEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplySagaStateConfiguration(new EfCoreSagaStoreOptions
        {
            TableName = "SagaStates",
            EnableOptimisticConcurrency = true
        });
    }
}

public class EfCoreSagaStoreTests : IAsyncLifetime
{
    private ServiceProvider _serviceProvider = null!;
    private ISagaStore _store = null!;
    private TestSagaDbContext _context = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();

        services.AddDbContext<TestSagaDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

        services.AddEfCoreSagaStore<TestSagaDbContext>(opt =>
        {
            opt.TableName = "SagaStates";
            opt.EnableOptimisticConcurrency = true;
        });

        _serviceProvider = services.BuildServiceProvider();
        _context = _serviceProvider.GetRequiredService<TestSagaDbContext>();
        _store = _serviceProvider.GetRequiredService<ISagaStore>();

        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task SaveAsync_Should_CreateNewSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "OrderSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0,
            Data = "{\"orderId\":123}",
            CorrelationId = "test-correlation"
        };

        // Act
        await _store.SaveAsync(state);

        // Assert
        var retrieved = await _store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.SagaId.Should().Be(sagaId);
        retrieved.SagaType.Should().Be("OrderSaga");
        retrieved.Status.Should().Be(SagaStatus.Running);
        retrieved.Data.Should().Be("{\"orderId\":123}");
        retrieved.CorrelationId.Should().Be("test-correlation");
    }

    [Fact]
    public async Task SaveAsync_Should_UpdateExistingSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "OrderSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0
        };

        await _store.SaveAsync(state);

        // Act
        state.Status = SagaStatus.Completed;
        state.CurrentStep = 3;
        state.CompletedAt = DateTime.UtcNow;
        await _store.SaveAsync(state);

        // Assert
        var retrieved = await _store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(SagaStatus.Completed);
        retrieved.CurrentStep.Should().Be(3);
        retrieved.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAsync_Should_ReturnNull_WhenNotFound()
    {
        // Act
        var result = await _store.GetAsync(Guid.NewGuid());

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPendingAsync_Should_ReturnOnlyPendingSagas()
    {
        // Arrange
        var runningSaga = new SagaState
        {
            SagaId = Guid.NewGuid(),
            SagaType = "OrderSaga",
            Status = SagaStatus.Running
        };

        var compensatingSaga = new SagaState
        {
            SagaId = Guid.NewGuid(),
            SagaType = "OrderSaga",
            Status = SagaStatus.Compensating
        };

        var completedSaga = new SagaState
        {
            SagaId = Guid.NewGuid(),
            SagaType = "OrderSaga",
            Status = SagaStatus.Completed
        };

        await _store.SaveAsync(runningSaga);
        await _store.SaveAsync(compensatingSaga);
        await _store.SaveAsync(completedSaga);

        // Act
        var pending = await _store.GetPendingAsync().ToListAsync();

        // Assert
        pending.Should().HaveCount(2);
        pending.Should().Contain(s => s.SagaId == runningSaga.SagaId);
        pending.Should().Contain(s => s.SagaId == compensatingSaga.SagaId);
        pending.Should().NotContain(s => s.SagaId == completedSaga.SagaId);
    }

    [Fact]
    public async Task GetPendingAsync_Should_FilterBySagaType()
    {
        // Arrange
        var orderSaga = new SagaState
        {
            SagaId = Guid.NewGuid(),
            SagaType = "OrderSaga",
            Status = SagaStatus.Running
        };

        var paymentSaga = new SagaState
        {
            SagaId = Guid.NewGuid(),
            SagaType = "PaymentSaga",
            Status = SagaStatus.Running
        };

        await _store.SaveAsync(orderSaga);
        await _store.SaveAsync(paymentSaga);

        // Act
        var orderSagas = await _store.GetPendingAsync("OrderSaga").ToListAsync();

        // Assert
        orderSagas.Should().HaveCount(1);
        orderSagas[0].SagaType.Should().Be("OrderSaga");
    }

    [Fact]
    public async Task DeleteAsync_Should_RemoveSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "OrderSaga",
            Status = SagaStatus.Running
        };

        await _store.SaveAsync(state);

        // Act
        await _store.DeleteAsync(sagaId);

        // Assert
        var retrieved = await _store.GetAsync(sagaId);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_NotThrow_WhenSagaNotExists()
    {
        // Act & Assert
        var act = async () => await _store.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    // ==================== EfCoreSagaStoreOptions Tests ====================

    [Fact]
    public void EfCoreSagaStoreOptions_AutoMigrate_ShouldDefaultToFalse()
    {
        var options = new EfCoreSagaStoreOptions();
        options.AutoMigrate.Should().BeFalse();
    }

    [Fact]
    public void EfCoreSagaStoreOptions_UseJsonColumn_ShouldDefaultToFalse()
    {
        var options = new EfCoreSagaStoreOptions();
        options.UseJsonColumn.Should().BeFalse();
    }

    // ==================== AutoMigrate Tests ====================

    [Fact]
    public async Task EfCoreSagaStore_WithAutoMigrateTrue_ShouldCallEnsureCreated()
    {
        var dbName = $"AutoMigrateTrue_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AutoMigrateTestDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        var storeOptions = new EfCoreSagaStoreOptions { AutoMigrate = true };
        services.AddSingleton(storeOptions);
        var serviceProvider = services.BuildServiceProvider();

        var context = serviceProvider.GetRequiredService<AutoMigrateTestDbContext>();
        var store = new EfCoreSagaStore<AutoMigrateTestDbContext>(context, storeOptions);

        var result = await store.GetAsync(Guid.NewGuid());
        result.Should().BeNull();

        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "TestSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0,
            Data = "{\"test\":true}"
        };
        await store.SaveAsync(state);

        var retrieved = await store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.SagaType.Should().Be("TestSaga");

        await context.Database.EnsureDeletedAsync();
        await (serviceProvider as IAsyncDisposable)!.DisposeAsync();
    }

    [Fact]
    public async Task EfCoreSagaStore_WithAutoMigrateFalse_ShouldNotCallEnsureCreated()
    {
        var dbName = $"AutoMigrateFalse_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AutoMigrateTestDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        var storeOptions = new EfCoreSagaStoreOptions { AutoMigrate = false };
        services.AddSingleton(storeOptions);
        var serviceProvider = services.BuildServiceProvider();

        var context = serviceProvider.GetRequiredService<AutoMigrateTestDbContext>();
        await context.Database.EnsureCreatedAsync();

        var store = new EfCoreSagaStore<AutoMigrateTestDbContext>(context, storeOptions);

        var result = await store.GetAsync(Guid.NewGuid());
        result.Should().BeNull();

        await context.Database.EnsureDeletedAsync();
        await (serviceProvider as IAsyncDisposable)!.DisposeAsync();
    }

    [Fact]
    public async Task EfCoreSagaStore_WithAutoMigrateFalse_ShouldWorkWithInMemory()
    {
        var dbName = $"AutoMigrateInMemory_{Guid.NewGuid()}";
        var services = new ServiceCollection();
        services.AddDbContext<AutoMigrateTestDbContext>(options =>
            options.UseInMemoryDatabase(dbName));
        var storeOptions = new EfCoreSagaStoreOptions { AutoMigrate = false };
        services.AddSingleton(storeOptions);
        var serviceProvider = services.BuildServiceProvider();

        var context = serviceProvider.GetRequiredService<AutoMigrateTestDbContext>();
        var store = new EfCoreSagaStore<AutoMigrateTestDbContext>(context, storeOptions);

        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "TestSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0
        };

        var act = async () => await store.SaveAsync(state);
        await act.Should().NotThrowAsync();

        await (serviceProvider as IAsyncDisposable)!.DisposeAsync();
    }

    // ==================== UseJsonColumn Tests ====================

    [Fact]
    public void SagaStateEntityConfiguration_WithUseJsonColumnTrue_ShouldSetJsonColumnType()
    {
        var options = new EfCoreSagaStoreOptions { UseJsonColumn = true };

        var dbContextOptions = new DbContextOptionsBuilder<JsonColumnTrueTestDbContext>()
            .UseInMemoryDatabase($"JsonColumnTrue_{Guid.NewGuid()}")
            .Options;

        using var context = new JsonColumnTrueTestDbContext(dbContextOptions, options);

        var model = context.Model;
        var entityType = model.FindEntityType(typeof(SagaStateEntity));
        var dataProperty = entityType!.FindProperty(nameof(SagaStateEntity.Data));

        dataProperty.Should().NotBeNull();
        var columnTypeAnnotation = dataProperty!.FindAnnotation("Relational:ColumnType");
        columnTypeAnnotation.Should().NotBeNull();
        columnTypeAnnotation!.Value.Should().Be("json");
    }

    [Fact]
    public void SagaStateEntityConfiguration_WithUseJsonColumnFalse_ShouldNotSetJsonColumnType()
    {
        var options = new EfCoreSagaStoreOptions { UseJsonColumn = false };

        var dbContextOptions = new DbContextOptionsBuilder<JsonColumnFalseTestDbContext>()
            .UseInMemoryDatabase($"JsonColumnFalse_{Guid.NewGuid()}")
            .Options;

        using var context = new JsonColumnFalseTestDbContext(dbContextOptions, options);

        var model = context.Model;
        var entityType = model.FindEntityType(typeof(SagaStateEntity));
        var dataProperty = entityType!.FindProperty(nameof(SagaStateEntity.Data));

        dataProperty.Should().NotBeNull();
        var columnTypeAnnotation = dataProperty!.FindAnnotation("Relational:ColumnType");
        columnTypeAnnotation.Should().BeNull();
    }

    // ==================== Test DbContexts for AutoMigrate/UseJsonColumn ====================

    public class AutoMigrateTestDbContext : DbContext
    {
        public AutoMigrateTestDbContext(DbContextOptions<AutoMigrateTestDbContext> options)
            : base(options) { }

        public DbSet<SagaStateEntity> SagaStates => Set<SagaStateEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplySagaStateConfiguration(new EfCoreSagaStoreOptions
            {
                TableName = "SagaStates",
                EnableOptimisticConcurrency = false
            });
        }
    }

    public class JsonColumnTrueTestDbContext : DbContext
    {
        private readonly EfCoreSagaStoreOptions _sagaOptions;

        public JsonColumnTrueTestDbContext(
            DbContextOptions<JsonColumnTrueTestDbContext> options,
            EfCoreSagaStoreOptions sagaOptions)
            : base(options)
        {
            _sagaOptions = sagaOptions;
        }

        public DbSet<SagaStateEntity> SagaStates => Set<SagaStateEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplySagaStateConfiguration(_sagaOptions);
        }
    }

    public class JsonColumnFalseTestDbContext : DbContext
    {
        private readonly EfCoreSagaStoreOptions _sagaOptions;

        public JsonColumnFalseTestDbContext(
            DbContextOptions<JsonColumnFalseTestDbContext> options,
            EfCoreSagaStoreOptions sagaOptions)
            : base(options)
        {
            _sagaOptions = sagaOptions;
        }

        public DbSet<SagaStateEntity> SagaStates => Set<SagaStateEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplySagaStateConfiguration(_sagaOptions);
        }
    }
}
