namespace TurboMediator.Results;

/// <summary>
/// Static factory methods for creating <see cref="Result{TValue}"/> and <see cref="Result{TValue, TError}"/> instances.
/// </summary>
public static class Result
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static Result<TValue> Success<TValue>(TValue value) => Result<TValue>.Success(value);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static Result<TValue> Failure<TValue>(Exception error) => Result<TValue>.Failure(error);

    /// <summary>
    /// Creates a failed result with a message.
    /// </summary>
    public static Result<TValue> Failure<TValue>(string errorMessage) => Result<TValue>.Failure(errorMessage);

    /// <summary>
    /// Creates a successful result with typed error.
    /// </summary>
    public static Result<TValue, TError> Success<TValue, TError>(TValue value) => Result<TValue, TError>.Success(value);

    /// <summary>
    /// Creates a failed result with typed error.
    /// </summary>
    public static Result<TValue, TError> Failure<TValue, TError>(TError error) => Result<TValue, TError>.Failure(error);

    /// <summary>
    /// Executes a function and wraps the result, catching any exceptions.
    /// </summary>
    public static Result<TValue> Try<TValue>(Func<TValue> func)
    {
        try
        {
            return Result<TValue>.Success(func());
        }
        catch (Exception ex)
        {
            return Result<TValue>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an async function and wraps the result, catching any exceptions.
    /// </summary>
    public static async ValueTask<Result<TValue>> TryAsync<TValue>(Func<ValueTask<TValue>> func)
    {
        try
        {
            return Result<TValue>.Success(await func());
        }
        catch (Exception ex)
        {
            return Result<TValue>.Failure(ex);
        }
    }

    /// <summary>
    /// Executes an async function returning Task and wraps the result, catching any exceptions.
    /// </summary>
    public static async ValueTask<Result<TValue>> TryAsync<TValue>(Func<Task<TValue>> func)
    {
        try
        {
            return Result<TValue>.Success(await func());
        }
        catch (Exception ex)
        {
            return Result<TValue>.Failure(ex);
        }
    }
}
