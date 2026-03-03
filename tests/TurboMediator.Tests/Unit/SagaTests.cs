using FluentAssertions;
using TurboMediator.Saga;
using Xunit;

namespace TurboMediator.Tests;

public class SagaTests
{
    // Test saga data
    public class OrderSagaData
    {
        public string OrderId { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool InventoryReserved { get; set; }
        public bool PaymentProcessed { get; set; }
        public bool ShipmentScheduled { get; set; }
    }

    // Test commands with responses
    public class ReserveInventoryCommand : ICommand<bool>
    {
        public string OrderId { get; set; } = string.Empty;
    }

    public class ProcessPaymentCommand : ICommand<bool>
    {
        public string OrderId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }

    public class ScheduleShipmentCommand : ICommand<bool>
    {
        public string OrderId { get; set; } = string.Empty;
    }

    public class ReleaseInventoryCommand : ICommand<Unit> { }
    public class RefundPaymentCommand : ICommand<Unit> { }
    public class CancelShipmentCommand : ICommand<Unit> { }

    // Test saga using custom execute functions
    public class OrderSaga : Saga<OrderSagaData>
    {
        public bool ShouldFailInventory { get; set; }
        public bool ShouldFailPayment { get; set; }
        public bool ShouldFailShipment { get; set; }

        public OrderSaga()
        {
            AddStep(Step("Reserve Inventory")
                .Execute(async (mediator, data, ct) =>
                {
                    if (ShouldFailInventory) return false;
                    var result = await mediator.Send(new ReserveInventoryCommand { OrderId = data.OrderId }, ct);
                    data.InventoryReserved = result;
                    return result;
                })
                .Compensate(async (mediator, data, ct) =>
                {
                    await mediator.Send(new ReleaseInventoryCommand(), ct);
                    data.InventoryReserved = false;
                }));

            AddStep(Step("Process Payment")
                .Execute(async (mediator, data, ct) =>
                {
                    if (ShouldFailPayment) return false;
                    var result = await mediator.Send(new ProcessPaymentCommand { OrderId = data.OrderId, Amount = data.Amount }, ct);
                    data.PaymentProcessed = result;
                    return result;
                })
                .Compensate(async (mediator, data, ct) =>
                {
                    await mediator.Send(new RefundPaymentCommand(), ct);
                    data.PaymentProcessed = false;
                }));

            AddStep(Step("Schedule Shipment")
                .Execute(async (mediator, data, ct) =>
                {
                    if (ShouldFailShipment) return false;
                    var result = await mediator.Send(new ScheduleShipmentCommand { OrderId = data.OrderId }, ct);
                    data.ShipmentScheduled = result;
                    return result;
                })
                .Compensate(async (mediator, data, ct) =>
                {
                    await mediator.Send(new CancelShipmentCommand(), ct);
                    data.ShipmentScheduled = false;
                }));
        }
    }

    // Mock mediator for testing
    private class MockMediator : IMediator
    {
        public List<object> SentCommands { get; } = new();

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            SentCommands.Add(command);
            // Return true for bool, Unit for Unit
            if (typeof(TResponse) == typeof(bool))
            {
                return new ValueTask<TResponse>((TResponse)(object)true);
            }
            return new ValueTask<TResponse>((TResponse)(object)Unit.Value);
        }

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }

    [Fact]
    public void Saga_ShouldHaveCorrectSteps()
    {
        var saga = new OrderSaga();
        saga.Steps.Should().HaveCount(3);
        saga.Steps[0].Name.Should().Be("Reserve Inventory");
        saga.Steps[1].Name.Should().Be("Process Payment");
        saga.Steps[2].Name.Should().Be("Schedule Shipment");
    }

    [Fact]
    public async Task SagaOrchestrator_ShouldExecuteAllStepsSuccessfully()
    {
        var mediator = new MockMediator();
        var store = new InMemorySagaStore();
        var orchestrator = new SagaOrchestrator<OrderSagaData>(mediator, store);
        var saga = new OrderSaga();
        var data = new OrderSagaData
        {
            OrderId = "ORD-001",
            CustomerId = "CUST-001",
            Amount = 100m
        };

        var result = await orchestrator.ExecuteAsync(saga, data);

        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.InventoryReserved.Should().BeTrue();
        result.Data.PaymentProcessed.Should().BeTrue();
        result.Data.ShipmentScheduled.Should().BeTrue();
        mediator.SentCommands.Should().HaveCount(3);
    }

    [Fact]
    public async Task SagaOrchestrator_ShouldCompensateOnFailure()
    {
        var mediator = new MockMediator();
        var store = new InMemorySagaStore();
        var orchestrator = new SagaOrchestrator<OrderSagaData>(mediator, store);
        var saga = new OrderSaga { ShouldFailShipment = true };
        var data = new OrderSagaData
        {
            OrderId = "ORD-002",
            CustomerId = "CUST-002",
            Amount = 200m
        };

        var result = await orchestrator.ExecuteAsync(saga, data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("Step returned false");
        // Should have executed: Reserve, Payment, Shipment (failed)
        // Then compensated: Payment, Inventory
        mediator.SentCommands.Should().HaveCountGreaterThan(3);
    }

    [Fact]
    public async Task InMemorySagaStore_ShouldPersistSagaState()
    {
        var store = new InMemorySagaStore();
        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "TestSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0,
            Data = "test",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await store.SaveAsync(state);
        var retrieved = await store.GetAsync(sagaId);

        retrieved.Should().NotBeNull();
        retrieved!.SagaId.Should().Be(sagaId);
        retrieved.Status.Should().Be(SagaStatus.Running);
    }

    [Fact]
    public async Task InMemorySagaStore_ShouldRetrievePendingSagas()
    {
        var store = new InMemorySagaStore();

        var runningState = new SagaState
        {
            SagaId = Guid.NewGuid(),
            SagaType = "TestSaga",
            Status = SagaStatus.Running,
            CurrentStep = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var completedState = new SagaState
        {
            SagaId = Guid.NewGuid(),
            SagaType = "TestSaga",
            Status = SagaStatus.Completed,
            CurrentStep = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await store.SaveAsync(runningState);
        await store.SaveAsync(completedState);

        var pending = new List<SagaState>();
        await foreach (var state in store.GetPendingAsync())
        {
            pending.Add(state);
        }

        pending.Should().HaveCount(1);
        pending[0].SagaId.Should().Be(runningState.SagaId);
    }

    [Fact]
    public async Task InMemorySagaStore_ShouldDeleteSaga()
    {
        var store = new InMemorySagaStore();
        var sagaId = Guid.NewGuid();
        var state = new SagaState
        {
            SagaId = sagaId,
            SagaType = "TestSaga",
            Status = SagaStatus.Completed,
            CurrentStep = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await store.SaveAsync(state);
        await store.DeleteAsync(sagaId);
        var retrieved = await store.GetAsync(sagaId);

        retrieved.Should().BeNull();
    }

    [Fact]
    public void SagaState_ShouldHaveCorrectStatusEnum()
    {
        var state = new SagaState();

        state.Status = SagaStatus.NotStarted;
        state.Status.Should().Be(SagaStatus.NotStarted);

        state.Status = SagaStatus.Running;
        state.Status.Should().Be(SagaStatus.Running);

        state.Status = SagaStatus.Completed;
        state.Status.Should().Be(SagaStatus.Completed);

        state.Status = SagaStatus.Failed;
        state.Status.Should().Be(SagaStatus.Failed);

        state.Status = SagaStatus.Compensating;
        state.Status.Should().Be(SagaStatus.Compensating);

        state.Status = SagaStatus.Compensated;
        state.Status.Should().Be(SagaStatus.Compensated);
    }

    [Fact]
    public void SagaResult_ShouldReturnCorrectSuccessResult()
    {
        var sagaId = Guid.NewGuid();
        var data = new OrderSagaData { OrderId = "ORD-001" };

        var result = SagaResult<OrderSagaData>.Success(sagaId, data);

        result.IsSuccess.Should().BeTrue();
        result.SagaId.Should().Be(sagaId);
        result.Data.Should().NotBeNull();
        result.Data!.OrderId.Should().Be("ORD-001");
        result.Error.Should().BeNull();
        result.CompensationErrors.Should().BeEmpty();
    }

    [Fact]
    public void SagaResult_ShouldReturnCorrectFailureResult()
    {
        var sagaId = Guid.NewGuid();
        var compensationErrors = new List<string> { "Error 1", "Error 2" };

        var result = SagaResult<OrderSagaData>.Failure(sagaId, "Main error", compensationErrors);

        result.IsSuccess.Should().BeFalse();
        result.SagaId.Should().Be(sagaId);
        result.Error.Should().Be("Main error");
        result.CompensationErrors.Should().HaveCount(2);
    }

    [Fact]
    public void SagaStep_ShouldHaveCompensationFlag()
    {
        var saga = new OrderSaga();
        foreach (var step in saga.Steps)
        {
            step.HasCompensation.Should().BeTrue();
        }
    }
}
