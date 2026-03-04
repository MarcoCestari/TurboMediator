using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace TurboMediator.StateMachine;

/// <summary>
/// Abstract base class for defining a state machine. Subclass this to configure
/// states, transitions, guards, and entry/exit actions.
/// </summary>
/// <typeparam name="TEntity">The entity type implementing <see cref="IStateful{TState}"/>.</typeparam>
/// <typeparam name="TState">The state enum type.</typeparam>
/// <typeparam name="TTrigger">The trigger enum type.</typeparam>
public abstract class StateMachine<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] TEntity, TState, TTrigger> : IStateMachine<TEntity, TState, TTrigger>
    where TEntity : IStateful<TState>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly StateMachineBuilder<TEntity, TState, TTrigger> _builder;
    private readonly IMediator _mediator;
    private readonly ITransitionStore? _transitionStore;
    private readonly ILogger _logger;
    private readonly Func<TEntity, string> _entityIdSelector;
    private bool _configured;

    /// <summary>
    /// Initializes a new instance of the state machine.
    /// </summary>
    /// <param name="mediator">The mediator for dispatching commands/notifications.</param>
    /// <param name="transitionStore">Optional transition store for auditing.</param>
    /// <param name="logger">Optional logger.</param>
    protected StateMachine(
        IMediator mediator,
        ITransitionStore? transitionStore = null,
        ILogger? logger = null)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _transitionStore = transitionStore;
        _logger = logger ?? NullLogger.Instance;
        _builder = new StateMachineBuilder<TEntity, TState, TTrigger>();
        _entityIdSelector = DefaultEntityIdSelector;
    }

    /// <summary>
    /// Gets the name of the state machine. Defaults to the type name.
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    /// Gets the configured initial state.
    /// </summary>
    public TState InitialState
    {
        get
        {
            EnsureConfigured();
            return _builder.ConfiguredInitialState!.Value;
        }
    }

    /// <summary>
    /// Override this method to configure the state machine using the fluent builder API.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    protected abstract void Configure(StateMachineBuilder<TEntity, TState, TTrigger> builder);

    /// <summary>
    /// Override this to provide a custom entity ID selector for transition records.
    /// Default attempts to find an "Id" property.
    /// </summary>
    /// <param name="entity">The entity.</param>
    /// <returns>A string representation of the entity's identity.</returns>
    protected virtual string GetEntityId(TEntity entity) => _entityIdSelector(entity);

    /// <inheritdoc />
    public async Task<TransitionResult<TState, TTrigger>> FireAsync(
        TEntity entity,
        TTrigger trigger,
        IDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureConfigured();

        var currentState = entity.CurrentState;

        // Find the transition definition for this state+trigger
        if (!_builder.States.TryGetValue(currentState, out var stateConfig))
        {
            return HandleInvalidTransition(entity, currentState, trigger);
        }

        var transition = stateConfig.Transitions
            .FirstOrDefault(t => EqualityComparer<TTrigger>.Default.Equals(t.Trigger, trigger));

        if (transition == null)
        {
            return HandleInvalidTransition(entity, currentState, trigger);
        }

        // Evaluate guard conditions
        if (!transition.EvaluateGuards(entity))
        {
            var guardDescs = transition.Guards
                .Where(g => g.Description != null)
                .Select(g => g.Description!)
                .ToList();

            var guardInfo = guardDescs.Count > 0
                ? $" Guards not satisfied: [{string.Join(", ", guardDescs)}]"
                : " Guard condition not satisfied.";

            _logger.LogWarning(
                "State machine '{Name}': Trigger '{Trigger}' blocked on entity in state '{State}'.{GuardInfo}",
                Name, trigger, currentState, guardInfo);

            return TransitionResult<TState, TTrigger>.Failure(
                currentState, trigger,
                $"Guard condition not satisfied for trigger '{trigger}' in state '{currentState}'.{guardInfo}");
        }

        var destinationState = transition.DestinationState;
        var context = new TransitionContext(_mediator, metadata);

        // Execute exit action of current state
        if (stateConfig.ExitAction != null)
        {
            await stateConfig.ExitAction(entity, context).ConfigureAwait(false);
        }

        // Transition the entity
        entity.CurrentState = destinationState;

        // Execute entry action of destination state
        if (_builder.States.TryGetValue(destinationState, out var destConfig) && destConfig.EntryAction != null)
        {
            await destConfig.EntryAction(entity, context).ConfigureAwait(false);
        }

        // Execute global on-transition callback
        if (_builder.GlobalOnTransition != null)
        {
            await _builder.GlobalOnTransition(entity, currentState, destinationState, trigger).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "State machine '{Name}': {FromState} -> {ToState} via '{Trigger}'",
            Name, currentState, destinationState, trigger);

        // Persist transition record if store is available
        if (_transitionStore != null)
        {
            var record = new TransitionRecord<TState, TTrigger>
            {
                EntityId = GetEntityId(entity),
                StateMachineType = Name,
                FromState = currentState,
                ToState = destinationState,
                Trigger = trigger,
                Timestamp = DateTime.UtcNow,
                Metadata = metadata?.AsReadOnly()
            };
            await _transitionStore.SaveAsync(record, cancellationToken).ConfigureAwait(false);
        }

        return TransitionResult<TState, TTrigger>.Success(currentState, destinationState, trigger);
    }

    /// <inheritdoc />
    public IReadOnlyList<TTrigger> GetPermittedTriggers(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureConfigured();

        var currentState = entity.CurrentState;

        if (!_builder.States.TryGetValue(currentState, out var stateConfig))
            return Array.Empty<TTrigger>();

        return stateConfig.Transitions
            .Where(t => t.EvaluateGuards(entity))
            .Select(t => t.Trigger)
            .Distinct()
            .ToList();
    }

    /// <inheritdoc />
    public bool CanFire(TEntity entity, TTrigger trigger)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureConfigured();

        var currentState = entity.CurrentState;

        if (!_builder.States.TryGetValue(currentState, out var stateConfig))
            return false;

        return stateConfig.Transitions
            .Any(t => EqualityComparer<TTrigger>.Default.Equals(t.Trigger, trigger) && t.EvaluateGuards(entity));
    }

    /// <inheritdoc />
    public IReadOnlyList<TState> GetAllStates()
    {
        EnsureConfigured();
        return _builder.States.Keys.ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<(TState Source, TTrigger Trigger, TState Destination, IReadOnlyList<string> Guards)> GetAllTransitions()
    {
        EnsureConfigured();

        var result = new List<(TState, TTrigger, TState, IReadOnlyList<string>)>();

        foreach (var kvp in _builder.States)
        {
            foreach (var transition in kvp.Value.Transitions)
            {
                var guards = transition.Guards
                    .Where(g => g.Description != null)
                    .Select(g => g.Description!)
                    .ToList();

                result.Add((transition.SourceState, transition.Trigger, transition.DestinationState, guards));
            }
        }

        return result;
    }

    /// <inheritdoc />
    public bool IsFinalState(TState state)
    {
        EnsureConfigured();
        return _builder.States.TryGetValue(state, out var config) && config.IsFinal;
    }

    /// <summary>
    /// Generates a Mermaid state diagram for this state machine.
    /// </summary>
    /// <returns>A Mermaid diagram string.</returns>
    public string ToMermaidDiagram()
    {
        EnsureConfigured();

        var lines = new List<string> { "stateDiagram-v2" };

        // Initial state arrow
        if (_builder.ConfiguredInitialState.HasValue)
        {
            lines.Add($"    [*] --> {_builder.ConfiguredInitialState.Value}");
        }

        // Transitions
        foreach (var (source, trigger, destination, guards) in GetAllTransitions())
        {
            var label = trigger.ToString();
            if (guards.Count > 0)
            {
                label += $" [{string.Join(", ", guards)}]";
            }
            lines.Add($"    {source} --> {destination} : {label}");
        }

        // Final states
        foreach (var kvp in _builder.States)
        {
            if (kvp.Value.IsFinal)
            {
                lines.Add($"    {kvp.Key} --> [*]");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private TransitionResult<TState, TTrigger> HandleInvalidTransition(TEntity entity, TState currentState, TTrigger trigger)
    {
        if (_builder.InvalidTransitionHandler != null)
        {
            _builder.InvalidTransitionHandler(entity, trigger);
            return TransitionResult<TState, TTrigger>.Failure(
                currentState, trigger,
                $"Invalid transition: trigger '{trigger}' is not permitted in state '{currentState}'.");
        }

        throw new InvalidTransitionException(typeof(TEntity).Name, currentState.ToString(), trigger.ToString());
    }

    private void EnsureConfigured()
    {
        if (_configured) return;

        Configure(_builder);
        _builder.Validate();
        _configured = true;
    }

    private static string DefaultEntityIdSelector(TEntity entity)
    {
        // Try to find an "Id" property via reflection
        var idProp = typeof(TEntity).GetProperty("Id")
                  ?? typeof(TEntity).GetProperty("ID")
                  ?? typeof(TEntity).GetProperty($"{typeof(TEntity).Name}Id");

        if (idProp != null)
        {
            return idProp.GetValue(entity)?.ToString() ?? "unknown";
        }

        return entity?.GetHashCode().ToString() ?? "unknown";
    }
}
