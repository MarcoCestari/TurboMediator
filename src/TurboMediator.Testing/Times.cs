using System;

namespace TurboMediator.Testing;

/// <summary>
/// Specifies the number of times a method should be called.
/// </summary>
public readonly struct Times : IEquatable<Times>
{
    private readonly int _min;
    private readonly int _max;
    private readonly string _description;

    private Times(int min, int max, string description)
    {
        _min = min;
        _max = max;
        _description = description;
    }

    /// <summary>
    /// Specifies that the method should never be called.
    /// </summary>
    public static Times Never() => new(0, 0, "never");

    /// <summary>
    /// Specifies that the method should be called exactly once.
    /// </summary>
    public static Times Once() => new(1, 1, "once");

    /// <summary>
    /// Specifies that the method should be called exactly the specified number of times.
    /// </summary>
    public static Times Exactly(int count) => new(count, count, $"exactly {count} time(s)");

    /// <summary>
    /// Specifies that the method should be called at least once.
    /// </summary>
    public static Times AtLeastOnce() => new(1, int.MaxValue, "at least once");

    /// <summary>
    /// Specifies that the method should be called at least the specified number of times.
    /// </summary>
    public static Times AtLeast(int count) => new(count, int.MaxValue, $"at least {count} time(s)");

    /// <summary>
    /// Specifies that the method should be called at most the specified number of times.
    /// </summary>
    public static Times AtMost(int count) => new(0, count, $"at most {count} time(s)");

    /// <summary>
    /// Specifies that the method should be called between the specified range of times.
    /// </summary>
    public static Times Between(int min, int max) => new(min, max, $"between {min} and {max} time(s)");

    /// <summary>
    /// Validates that the actual count matches the expected times.
    /// </summary>
    internal bool Validate(int actualCount) => actualCount >= _min && actualCount <= _max;

    /// <summary>
    /// Gets a description of the expected times.
    /// </summary>
    internal string Description => _description;

    /// <inheritdoc />
    public bool Equals(Times other) => _min == other._min && _max == other._max;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Times other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (_min * 397) ^ _max;
        }
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Times left, Times right) => left.Equals(right);

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Times left, Times right) => !left.Equals(right);
}
