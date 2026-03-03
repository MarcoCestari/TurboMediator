namespace TurboMediator.Results;

/// <summary>
/// Represents the result of an operation that can either succeed or fail.
/// </summary>
/// <typeparam name="TValue">The type of the success value.</typeparam>
public readonly struct Result<TValue> : IEquatable<Result<TValue>>
{
    private readonly TValue? _value;
    private readonly Exception? _error;

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
        : throw new InvalidOperationException($"Cannot access Value on a failed result. Error: {_error?.Message}");

    /// <summary>
    /// Gets the error exception.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the result is a success.</exception>
    public Exception Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("Cannot access Error on a successful result.");

    private Result(TValue value)
    {
        IsSuccess = true;
        _value = value;
        _error = null;
    }

    private Result(Exception error)
    {
        IsSuccess = false;
        _value = default;
        _error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<TValue> Success(TValue value) => new(value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<TValue> Failure(Exception error) => new(error);

    /// <summary>
    /// Creates a failed result with a message.
    /// </summary>
    public static Result<TValue> Failure(string errorMessage) => new(new ResultException(errorMessage));

    /// <summary>
    /// Implicit conversion from value to successful result.
    /// </summary>
    public static implicit operator Result<TValue>(TValue value) => Success(value);

    /// <summary>
    /// Implicit conversion from exception to failed result.
    /// </summary>
    public static implicit operator Result<TValue>(Exception error) => Failure(error);

    /// <summary>
    /// Matches the result to one of two functions based on success or failure.
    /// </summary>
    public TResult Match<TResult>(Func<TValue, TResult> onSuccess, Func<Exception, TResult> onFailure)
    {
        return IsSuccess ? onSuccess(_value!) : onFailure(_error!);
    }

    /// <summary>
    /// Matches the result to one of two actions based on success or failure.
    /// </summary>
    public void Match(Action<TValue> onSuccess, Action<Exception> onFailure)
    {
        if (IsSuccess)
            onSuccess(_value!);
        else
            onFailure(_error!);
    }

    /// <summary>
    /// Maps the success value to a new type.
    /// </summary>
    public Result<TNew> Map<TNew>(Func<TValue, TNew> mapper)
    {
        return IsSuccess ? Result<TNew>.Success(mapper(_value!)) : Result<TNew>.Failure(_error!);
    }

    /// <summary>
    /// Binds the success value to a new result.
    /// </summary>
    public Result<TNew> Bind<TNew>(Func<TValue, Result<TNew>> binder)
    {
        return IsSuccess ? binder(_value!) : Result<TNew>.Failure(_error!);
    }

    /// <summary>
    /// Gets the value or a default if the result is a failure.
    /// </summary>
    public TValue GetValueOrDefault(TValue defaultValue = default!)
    {
        return IsSuccess ? _value! : defaultValue;
    }

    /// <summary>
    /// Gets the value or throws the error exception if the result is a failure.
    /// </summary>
    public TValue GetValueOrThrow()
    {
        if (IsFailure)
            throw _error!;
        return _value!;
    }

    /// <inheritdoc />
    public bool Equals(Result<TValue> other)
    {
        if (IsSuccess != other.IsSuccess)
            return false;

        return IsSuccess
            ? EqualityComparer<TValue>.Default.Equals(_value, other._value)
            : EqualityComparer<Exception>.Default.Equals(_error, other._error);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<TValue> other && Equals(other);

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
            : $"Failure({_error?.Message})";
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Result<TValue> left, Result<TValue> right) => left.Equals(right);
    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Result<TValue> left, Result<TValue> right) => !left.Equals(right);
}
