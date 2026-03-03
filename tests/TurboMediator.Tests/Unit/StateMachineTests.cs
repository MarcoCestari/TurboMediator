using FluentAssertions;
using TurboMediator.StateMachine;
using Xunit;

namespace TurboMediator.Tests;

public class StateMachineTests
{
    // Test states and triggers
    public enum TestStatus { Draft, Active, Paused, Completed, Cancelled }
    public enum TestTrigger { Activate, Pause, Resume, Complete, Cancel }

    // Test entity
    public class TestEntity : IStateful<TestStatus>
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public TestStatus CurrentState { get; set; } = TestStatus.Draft;
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public bool WasActivated { get; set; }
        public bool WasPaused { get; set; }
        public string? CancellationReason { get; set; }
    }

    // Test state machine
    public class TestStateMachine : StateMachine<TestEntity, TestStatus, TestTrigger>
    {
        public bool EntryActionCalled { get; set; }
        public bool ExitActionCalled { get; set; }
        public bool GlobalTransitionCalled { get; set; }
        public (TestStatus From, TestStatus To, TestTrigger Trigger)? LastTransition { get; set; }

        public TestStateMachine(IMediator mediator, ITransitionStore? store = null)
            : base(mediator, store)
        {
        }

        protected override void Configure(StateMachineBuilder<TestEntity, TestStatus, TestTrigger> builder)
        {
            builder.InitialState(TestStatus.Draft);

            builder.State(TestStatus.Draft)
                .Permit(TestTrigger.Activate, TestStatus.Active)
                    .When(e => e.Value > 0, "Value > 0")
                .Permit(TestTrigger.Cancel, TestStatus.Cancelled);

            builder.State(TestStatus.Active)
                .OnEntry(async (entity, ctx) =>
                {
                    EntryActionCalled = true;
                    entity.WasActivated = true;
                    await Task.CompletedTask;
                })
                .OnExit(async (entity, ctx) =>
                {
                    ExitActionCalled = true;
                    await Task.CompletedTask;
                })
                .Permit(TestTrigger.Pause, TestStatus.Paused)
                .Permit(TestTrigger.Complete, TestStatus.Completed)
                .Permit(TestTrigger.Cancel, TestStatus.Cancelled);

            builder.State(TestStatus.Paused)
                .OnEntry(async (entity, ctx) =>
                {
                    entity.WasPaused = true;
                    await Task.CompletedTask;
                })
                .Permit(TestTrigger.Resume, TestStatus.Active)
                .Permit(TestTrigger.Cancel, TestStatus.Cancelled);

            builder.State(TestStatus.Completed)
                .AsFinal();

            builder.State(TestStatus.Cancelled)
                .OnEntry(async (entity, ctx) =>
                {
                    if (ctx.Metadata.TryGetValue("reason", out var reason))
                        entity.CancellationReason = reason;
                    await Task.CompletedTask;
                })
                .AsFinal();

            builder.OnTransition(async (entity, from, to, trigger) =>
            {
                GlobalTransitionCalled = true;
                LastTransition = (from, to, trigger);
                await Task.CompletedTask;
            });
        }
    }

    // Mock mediator for testing
    private class MockMediator : IMediator
    {
        public List<object> SentCommands { get; } = new();
        public List<object> PublishedNotifications { get; } = new();

        public ValueTask<TResponse> Send<TResponse>(IQuery<TResponse> query, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public ValueTask<TResponse> Send<TResponse>(ICommand<TResponse> command, CancellationToken ct = default)
        {
            SentCommands.Add(command);
            if (typeof(TResponse) == typeof(bool))
                return new ValueTask<TResponse>((TResponse)(object)true);
            if (typeof(TResponse) == typeof(string))
                return new ValueTask<TResponse>((TResponse)(object)"mock-result");
            return new ValueTask<TResponse>((TResponse)(object)Unit.Value);
        }

        public ValueTask Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification
        {
            PublishedNotifications.Add(notification!);
            return ValueTask.CompletedTask;
        }

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamCommand<TResponse> command, CancellationToken ct = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamQuery<TResponse> query, CancellationToken ct = default)
            => throw new NotImplementedException();
    }

    private TestStateMachine CreateMachine(ITransitionStore? store = null)
        => new(new MockMediator(), store);

    // =============================================================
    // Basic transition tests
    // =============================================================

    [Fact]
    public void InitialState_ShouldBeConfigured()
    {
        var machine = CreateMachine();
        machine.InitialState.Should().Be(TestStatus.Draft);
    }

    [Fact]
    public async Task FireAsync_ShouldTransitionToNewState()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        var result = await machine.FireAsync(entity, TestTrigger.Activate);

        result.IsSuccess.Should().BeTrue();
        result.PreviousState.Should().Be(TestStatus.Draft);
        result.CurrentState.Should().Be(TestStatus.Active);
        result.Trigger.Should().Be(TestTrigger.Activate);
        entity.CurrentState.Should().Be(TestStatus.Active);
    }

    [Fact]
    public async Task FireAsync_ShouldExecuteEntryAction()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        await machine.FireAsync(entity, TestTrigger.Activate);

        machine.EntryActionCalled.Should().BeTrue();
        entity.WasActivated.Should().BeTrue();
    }

    [Fact]
    public async Task FireAsync_ShouldExecuteExitAction()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        await machine.FireAsync(entity, TestTrigger.Activate);
        await machine.FireAsync(entity, TestTrigger.Pause);

        machine.ExitActionCalled.Should().BeTrue();
    }

    [Fact]
    public async Task FireAsync_ShouldExecuteGlobalOnTransition()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        await machine.FireAsync(entity, TestTrigger.Activate);

        machine.GlobalTransitionCalled.Should().BeTrue();
        machine.LastTransition.Should().Be((TestStatus.Draft, TestStatus.Active, TestTrigger.Activate));
    }

    // =============================================================
    // Guard condition tests
    // =============================================================

    [Fact]
    public async Task FireAsync_ShouldFailWhenGuardNotSatisfied()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 0 }; // guard: Value > 0

        var result = await machine.FireAsync(entity, TestTrigger.Activate);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Guard condition not satisfied");
        entity.CurrentState.Should().Be(TestStatus.Draft); // state unchanged
    }

    [Fact]
    public void CanFire_ShouldReturnFalseWhenGuardNotSatisfied()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 0 };

        machine.CanFire(entity, TestTrigger.Activate).Should().BeFalse();
    }

    [Fact]
    public void CanFire_ShouldReturnTrueWhenGuardSatisfied()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        machine.CanFire(entity, TestTrigger.Activate).Should().BeTrue();
    }

    [Fact]
    public void GetPermittedTriggers_ShouldRespectGuards()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 0 };

        var triggers = machine.GetPermittedTriggers(entity);

        triggers.Should().Contain(TestTrigger.Cancel);
        triggers.Should().NotContain(TestTrigger.Activate); // guard fails
    }

    [Fact]
    public void GetPermittedTriggers_ShouldReturnAllWhenGuardsPass()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        var triggers = machine.GetPermittedTriggers(entity);

        triggers.Should().Contain(TestTrigger.Activate);
        triggers.Should().Contain(TestTrigger.Cancel);
    }

    // =============================================================
    // Invalid transition tests
    // =============================================================

    [Fact]
    public async Task FireAsync_ShouldThrowOnInvalidTransition()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        // Draft state does not have a Pause transition
        var act = () => machine.FireAsync(entity, TestTrigger.Pause);

        await act.Should().ThrowAsync<InvalidTransitionException>()
            .WithMessage("*Pause*Draft*");
    }

    [Fact]
    public async Task FireAsync_ShouldThrowOnFinalState()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        // Transition to Completed (final)
        await machine.FireAsync(entity, TestTrigger.Activate);
        await machine.FireAsync(entity, TestTrigger.Complete);

        // Try to fire from final state
        var act = () => machine.FireAsync(entity, TestTrigger.Activate);

        await act.Should().ThrowAsync<InvalidTransitionException>();
    }

    [Fact]
    public void CanFire_ShouldReturnFalseForInvalidTransition()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        machine.CanFire(entity, TestTrigger.Pause).Should().BeFalse();
    }

    // =============================================================
    // Final state tests
    // =============================================================

    [Fact]
    public void IsFinalState_ShouldReturnTrueForFinalStates()
    {
        var machine = CreateMachine();

        machine.IsFinalState(TestStatus.Completed).Should().BeTrue();
        machine.IsFinalState(TestStatus.Cancelled).Should().BeTrue();
    }

    [Fact]
    public void IsFinalState_ShouldReturnFalseForNonFinalStates()
    {
        var machine = CreateMachine();

        machine.IsFinalState(TestStatus.Draft).Should().BeFalse();
        machine.IsFinalState(TestStatus.Active).Should().BeFalse();
        machine.IsFinalState(TestStatus.Paused).Should().BeFalse();
    }

    // =============================================================
    // Metadata tests
    // =============================================================

    [Fact]
    public async Task FireAsync_ShouldPassMetadataToEntryAction()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        var metadata = new Dictionary<string, string> { ["reason"] = "No longer needed" };

        // Transition to Cancelled (which reads metadata)
        await machine.FireAsync(entity, TestTrigger.Cancel, metadata);

        entity.CancellationReason.Should().Be("No longer needed");
    }

    // =============================================================
    // Multi-step lifecycle tests
    // =============================================================

    [Fact]
    public async Task FullLifecycle_DraftToCompleted()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        // Draft → Active → Paused → Active → Completed
        await machine.FireAsync(entity, TestTrigger.Activate);
        entity.CurrentState.Should().Be(TestStatus.Active);

        await machine.FireAsync(entity, TestTrigger.Pause);
        entity.CurrentState.Should().Be(TestStatus.Paused);
        entity.WasPaused.Should().BeTrue();

        await machine.FireAsync(entity, TestTrigger.Resume);
        entity.CurrentState.Should().Be(TestStatus.Active);

        await machine.FireAsync(entity, TestTrigger.Complete);
        entity.CurrentState.Should().Be(TestStatus.Completed);
    }

    [Fact]
    public async Task FullLifecycle_DraftToCancelled()
    {
        var machine = CreateMachine();
        var entity = new TestEntity { Value = 100 };

        await machine.FireAsync(entity, TestTrigger.Cancel,
            new Dictionary<string, string> { ["reason"] = "Changed my mind" });

        entity.CurrentState.Should().Be(TestStatus.Cancelled);
        entity.CancellationReason.Should().Be("Changed my mind");
    }

    // =============================================================
    // Diagram / Introspection tests
    // =============================================================

    [Fact]
    public void GetAllStates_ShouldReturnAllConfiguredStates()
    {
        var machine = CreateMachine();

        var states = machine.GetAllStates();

        states.Should().HaveCount(5);
        states.Should().Contain(TestStatus.Draft);
        states.Should().Contain(TestStatus.Active);
        states.Should().Contain(TestStatus.Paused);
        states.Should().Contain(TestStatus.Completed);
        states.Should().Contain(TestStatus.Cancelled);
    }

    [Fact]
    public void GetAllTransitions_ShouldReturnAllConfiguredTransitions()
    {
        var machine = CreateMachine();

        var transitions = machine.GetAllTransitions();

        transitions.Should().HaveCountGreaterThanOrEqualTo(7); // 2 from Draft + 3 from Active + 2 from Paused
    }

    [Fact]
    public void ToMermaidDiagram_ShouldGenerateValidDiagram()
    {
        var machine = CreateMachine();

        var diagram = machine.ToMermaidDiagram();

        diagram.Should().Contain("stateDiagram-v2");
        diagram.Should().Contain("[*] --> Draft");
        diagram.Should().Contain("Draft --> Active : Activate");
        diagram.Should().Contain("Completed --> [*]");
        diagram.Should().Contain("Cancelled --> [*]");
    }

    [Fact]
    public void ToMermaidDiagram_ShouldIncludeGuardDescriptions()
    {
        var machine = CreateMachine();

        var diagram = machine.ToMermaidDiagram();

        diagram.Should().Contain("Value > 0");
    }

    // =============================================================
    // Transition store tests
    // =============================================================

    [Fact]
    public async Task FireAsync_ShouldPersistToTransitionStore()
    {
        var store = new InMemoryTransitionStore();
        var machine = CreateMachine(store);
        var entity = new TestEntity { Value = 100 };

        await machine.FireAsync(entity, TestTrigger.Activate);

        var history = new List<TransitionRecord<TestStatus, TestTrigger>>();
        await foreach (var record in store.GetHistoryAsync<TestStatus, TestTrigger>(entity.Id.ToString()))
        {
            history.Add(record);
        }

        history.Should().HaveCount(1);
        history[0].FromState.Should().Be(TestStatus.Draft);
        history[0].ToState.Should().Be(TestStatus.Active);
        history[0].Trigger.Should().Be(TestTrigger.Activate);
    }

    [Fact]
    public async Task FireAsync_ShouldNotPersistFailedTransition()
    {
        var store = new InMemoryTransitionStore();
        var machine = CreateMachine(store);
        var entity = new TestEntity { Value = 0 }; // guard fails

        await machine.FireAsync(entity, TestTrigger.Activate);

        var history = new List<TransitionRecord<TestStatus, TestTrigger>>();
        await foreach (var record in store.GetHistoryAsync<TestStatus, TestTrigger>(entity.Id.ToString()))
        {
            history.Add(record);
        }

        history.Should().BeEmpty();
    }
}

public class InMemoryTransitionStoreTests
{
    public enum S { A, B, C }
    public enum T { Go, Back }

    [Fact]
    public async Task SaveAsync_ShouldPersistRecord()
    {
        var store = new InMemoryTransitionStore();
        var record = new TransitionRecord<S, T>
        {
            EntityId = "entity-1",
            StateMachineType = "TestMachine",
            FromState = S.A,
            ToState = S.B,
            Trigger = T.Go
        };

        await store.SaveAsync(record);

        var history = new List<TransitionRecord<S, T>>();
        await foreach (var r in store.GetHistoryAsync<S, T>("entity-1"))
        {
            history.Add(r);
        }

        history.Should().HaveCount(1);
        history[0].FromState.Should().Be(S.A);
        history[0].ToState.Should().Be(S.B);
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnEmptyForUnknownEntity()
    {
        var store = new InMemoryTransitionStore();

        var history = new List<TransitionRecord<S, T>>();
        await foreach (var r in store.GetHistoryAsync<S, T>("unknown"))
        {
            history.Add(r);
        }

        history.Should().BeEmpty();
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldReturnMultipleRecords()
    {
        var store = new InMemoryTransitionStore();

        await store.SaveAsync(new TransitionRecord<S, T>
        {
            EntityId = "entity-1",
            FromState = S.A,
            ToState = S.B,
            Trigger = T.Go
        });

        await store.SaveAsync(new TransitionRecord<S, T>
        {
            EntityId = "entity-1",
            FromState = S.B,
            ToState = S.C,
            Trigger = T.Go
        });

        var history = new List<TransitionRecord<S, T>>();
        await foreach (var r in store.GetHistoryAsync<S, T>("entity-1"))
        {
            history.Add(r);
        }

        history.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetHistoryAsync_ShouldIsolateByEntityId()
    {
        var store = new InMemoryTransitionStore();

        await store.SaveAsync(new TransitionRecord<S, T>
        {
            EntityId = "entity-1",
            FromState = S.A,
            ToState = S.B,
            Trigger = T.Go
        });

        await store.SaveAsync(new TransitionRecord<S, T>
        {
            EntityId = "entity-2",
            FromState = S.A,
            ToState = S.C,
            Trigger = T.Go
        });

        var history1 = new List<TransitionRecord<S, T>>();
        await foreach (var r in store.GetHistoryAsync<S, T>("entity-1"))
        {
            history1.Add(r);
        }

        var history2 = new List<TransitionRecord<S, T>>();
        await foreach (var r in store.GetHistoryAsync<S, T>("entity-2"))
        {
            history2.Add(r);
        }

        history1.Should().HaveCount(1);
        history1[0].ToState.Should().Be(S.B);

        history2.Should().HaveCount(1);
        history2[0].ToState.Should().Be(S.C);
    }
}
