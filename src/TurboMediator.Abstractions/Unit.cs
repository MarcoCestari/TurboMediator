namespace TurboMediator;

/// <summary>
/// Represents a unit type (void equivalent) for handlers that don't return a value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    /// <summary>
    /// Gets the single value of the Unit type.
    /// </summary>
    public static readonly Unit Value = default;

    /// <summary>
    /// Returns a completed ValueTask with Unit value.
    /// </summary>
    public static ValueTask<Unit> ValueTask => new(Value);

    /// <summary>
    /// Returns a completed Task with Unit value.
    /// </summary>
    public static Task<Unit> Task => System.Threading.Tasks.Task.FromResult(Value);

    /// <inheritdoc />
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc />
    int IComparable.CompareTo(object? obj) => 0;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <summary>
    /// Determines whether two Unit instances are equal.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Determines whether two Unit instances are not equal.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
