namespace TurboMediator.StateMachine;

/// <summary>
/// Represents a guard condition that must be satisfied for a transition to occur.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
internal sealed class GuardClause<TEntity>
{
    public Func<TEntity, bool> Condition { get; }
    public string? Description { get; }

    public GuardClause(Func<TEntity, bool> condition, string? description = null)
    {
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        Description = description;
    }
}
