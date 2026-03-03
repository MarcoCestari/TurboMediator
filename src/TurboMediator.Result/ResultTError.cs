namespace TurboMediator.Results;

/// <summary>
/// Represents the result of an operation that can either succeed with a value or fail with a typed error.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
/// <typeparam name="TError">The type of the error.</typeparam>
public readonly struct Result<TValue, TError> : IEquatable<Result<TValue, TError>>
{
    private readonly TValue? _value;
    private readonly TError? _error;

    /// <summary>
    /// Gets a value indicating whether the result is a success.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the result is a failure.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the success value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a failure.</exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value on a failed result. Error: {_error}");

    /// <summary>
    /// Gets the error.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a success.</exception>
    public TError Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    private Result(TValue value, bool isSuccess)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = default;
    }

    private Result(TError error)
    {
        IsSuccess = false;
        _value = default;
        _error = error;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<TValue, TError> Success(TValue value) => new(value, true);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<TValue, TError> Failure(TError error) => new(error);

    /// <summary>
    /// Implicit conversion from value to successful result.
    /// </summary>
    public static implicit operator Result<TValue, TError>(TValue value) => Success(value);

    /// <summary>
    /// Implicit conversion from error to failed result.
    /// </summary>
    public static implicit operator Result<TValue, TError>(TError error) => Failure(error);

    /// <summary>
    /// Matches the result to one of two functions based on success or failure.
    /// </summary>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<TError, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    /// Matches the result to one of two actions based on success or failure.
    /// </summary>
    public void Match(Action<TValue> onSuccess, Action<TError> onFailure)
    {
        if (IsSuccess)
            onSuccess(_value!);
        else
            onFailure(_error!);
    }

    /// <summary>
    /// Maps the success value to a new type.
    /// </summary>
    public Result<TNew, TError> Map<TNew>(Func<TValue, TNew> mapper)
    {
        return IsSuccess ? Result<TNew, TError>.Success(mapper(_value!)) : Result<TNew, TError>.Failure(_error!);
    }

    /// <summary>
    /// Maps the error to a new type.
    /// </summary>
    public Result<TValue, TNewError> MapError<TNewError>(Func<TError, TNewError> mapper)
    {
        return IsFailure ? Result<TValue, TNewError>.Failure(mapper(_error!)) : Result<TValue, TNewError>.Success(_value!);
    }

    /// <summary>
    /// Binds the success value to a new result.
    /// </summary>
    public Result<TNew, TError> Bind<TNew>(Func<TValue, Result<TNew, TError>> binder)
    {
        return IsSuccess ? binder(_value!) : Result<TNew, TError>.Failure(_error!);
    }

    /// <summary>
    /// Gets the value or a default if the result is a failure.
    /// </summary>
    public TValue GetValueOrDefault(TValue defaultValue = default!)
    {
        return IsSuccess ? _value! : defaultValue;
    }

    /// <inheritdoc />
    public bool Equals(Result<TValue, TError> other)
    {
        if (IsSuccess != other.IsSuccess)
            return false;

        return IsSuccess
            ? EqualityComparer<TValue>.Default.Equals(_value, other._value)
            : EqualityComparer<TError>.Default.Equals(_error, other._error);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<TValue, TError> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            var hash = 17;
            hash = hash * 31 + IsSuccess.GetHashCode();
            hash = hash * 31 + (IsSuccess ? (_value?.GetHashCode() ?? 0) : (_error?.GetHashCode() ?? 0));
            return hash;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return IsSuccess
            ? $"Success({_value})"
            : $"Failure({_error})";
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Result<TValue, TError> left, Result<TValue, TError> right) => left.Equals(right);
    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Result<TValue, TError> left, Result<TValue, TError> right) => !left.Equals(right);
}
