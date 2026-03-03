namespace TurboMediator;

/// <summary>
/// Represents the result of exception handling.
/// </summary>
/// <typeparam name="TResponse">The type of response.</typeparam>
public readonly struct ExceptionHandlingResult<TResponse>
{
    /// <summary>
    /// Gets a value indicating whether the exception was handled.
    /// </summary>
    public bool Handled { get; }

    /// <summary>
    /// Gets the response to use if the exception was handled.
    /// </summary>
    public TResponse? Response { get; }

    private ExceptionHandlingResult(bool handled, TResponse? response)
    {
        Handled = handled;
        Response = response;
    }

    /// <summary>
    /// Creates a result indicating the exception was handled with the specified response.
    /// </summary>
    /// <param name="response">The response to return.</param>
    /// <returns>A handled result with the response.</returns>
    public static ExceptionHandlingResult<TResponse> HandledWith(TResponse response) =>
        new(true, response);

    /// <summary>
    /// Creates a result indicating the exception was not handled and should be rethrown.
    /// </summary>
    /// <returns>A not handled result.</returns>
    public static ExceptionHandlingResult<TResponse> NotHandled() =>
        new(false, default);
}
