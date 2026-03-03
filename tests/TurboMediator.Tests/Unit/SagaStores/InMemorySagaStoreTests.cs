using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TurboMediator.Saga;
using Xunit;

namespace TurboMediator.Tests.SagaStores;

/// <summary>
/// Tests for InMemorySagaStore to validate the ISagaStore contract.
/// These tests serve as a reference for the store contract that all implementations must follow.
/// </summary>
public class InMemorySagaStoreTests
{
    private readonly InMemorySagaStore _store = new();

    [Fact]
    public async Task SaveAsync_Should_CreateNewSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        var state = CreateSagaState(sagaId, "TestSaga", SagaStatus.Running);

        // Act
        await _store.SaveAsync(state);

        // Assert
        var retrieved = await _store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.SagaId.Should().Be(sagaId);
        retrieved.SagaType.Should().Be("TestSaga");
        retrieved.Status.Should().Be(SagaStatus.Running);
    }

    [Fact]
    public async Task SaveAsync_Should_UpdateExistingSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        var state = CreateSagaState(sagaId, "TestSaga", SagaStatus.Running);
        await _store.SaveAsync(state);

        // Act
        state.Status = SagaStatus.Completed;
        state.CurrentStep = 5;
        await _store.SaveAsync(state);

        // Assert
        var retrieved = await _store.GetAsync(sagaId);
        retrieved.Should().NotBeNull();
        retrieved!.Status.Should().Be(SagaStatus.Completed);
        retrieved.CurrentStep.Should().Be(5);
    }

    [Fact]
    public async Task SaveAsync_Should_UpdateTimestamp()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        var state = CreateSagaState(sagaId, "TestSaga", SagaStatus.Running);
        var beforeSave = DateTime.UtcNow;

        // Act
        await _store.SaveAsync(state);

        // Assert
        var retrieved = await _store.GetAsync(sagaId);
        retrieved!.UpdatedAt.Should().BeOnOrAfter(beforeSave);
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
    public async Task GetPendingAsync_Should_ReturnRunningAndCompensatingSagas()
    {
        // Arrange
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "Saga1", SagaStatus.NotStarted));
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "Saga2", SagaStatus.Running));
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "Saga3", SagaStatus.Compensating));
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "Saga4", SagaStatus.Completed));
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "Saga5", SagaStatus.Failed));

        // Act
        var pending = await _store.GetPendingAsync().ToListAsync();

        // Assert
        pending.Should().HaveCount(2);
        pending.Should().OnlyContain(s => s.Status == SagaStatus.Running || s.Status == SagaStatus.Compensating);
    }

    [Fact]
    public async Task GetPendingAsync_Should_FilterBySagaType()
    {
        // Arrange
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "OrderSaga", SagaStatus.Running));
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "PaymentSaga", SagaStatus.Running));
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "OrderSaga", SagaStatus.Running));

        // Act
        var orderSagas = await _store.GetPendingAsync("OrderSaga").ToListAsync();

        // Assert
        orderSagas.Should().HaveCount(2);
        orderSagas.Should().OnlyContain(s => s.SagaType == "OrderSaga");
    }

    [Fact]
    public async Task GetPendingAsync_Should_ReturnAll_WhenNoTypeFilter()
    {
        // Arrange
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "OrderSaga", SagaStatus.Running));
        await _store.SaveAsync(CreateSagaState(Guid.NewGuid(), "PaymentSaga", SagaStatus.Running));

        // Act
        var all = await _store.GetPendingAsync().ToListAsync();

        // Assert
        all.Should().HaveCount(2);
    }

    [Fact]
    public async Task DeleteAsync_Should_RemoveSaga()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        await _store.SaveAsync(CreateSagaState(sagaId, "TestSaga", SagaStatus.Running));

        // Act
        await _store.DeleteAsync(sagaId);

        // Assert
        var retrieved = await _store.GetAsync(sagaId);
        retrieved.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_Should_NotThrow_WhenNotExists()
    {
        // Act & Assert
        var act = async () => await _store.DeleteAsync(Guid.NewGuid());
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_Should_RemoveFromPending()
    {
        // Arrange
        var sagaId = Guid.NewGuid();
        await _store.SaveAsync(CreateSagaState(sagaId, "TestSaga", SagaStatus.Running));

        // Act
        await _store.DeleteAsync(sagaId);

        // Assert
        var pending = await _store.GetPendingAsync().ToListAsync();
        pending.Should().NotContain(s => s.SagaId == sagaId);
    }

    [Fact]
    public async Task Concurrent_SaveAndGet_Should_BeThreadSafe()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 100)
            .Select(async i =>
            {
                var sagaId = Guid.NewGuid();
                var state = CreateSagaState(sagaId, $"Saga{i}", SagaStatus.Running);
                await _store.SaveAsync(state);
                return await _store.GetAsync(sagaId);
            });

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().OnlyContain(r => r != null);
    }

    private static SagaState CreateSagaState(Guid sagaId, string sagaType, SagaStatus status)
    {
        return new SagaState
        {
            SagaId = sagaId,
            SagaType = sagaType,
            Status = status,
            CurrentStep = 0,
            CreatedAt = DateTime.UtcNow
        };
    }
}
