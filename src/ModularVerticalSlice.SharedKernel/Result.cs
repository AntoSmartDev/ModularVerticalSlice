namespace ModularVerticalSlice.SharedKernel;

/// <summary>
/// Represents the outcome of an operation that does not return a value.
/// </summary>
public class Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result" /> class.
    /// </summary>
    /// <param name="isSuccess">Indicates whether the operation succeeded.</param>
    /// <param name="error">The application error associated with the result.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the error state is inconsistent with the result outcome.
    /// </exception>
    protected Result(bool isSuccess, Error error)
    {
        if ((isSuccess && error != Error.None) ||
            (!isSuccess && error == Error.None))
        {
            throw new ArgumentException(
                "The error state is inconsistent with the result outcome.",
                nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    /// Gets the application error associated with the result.
    /// </summary>
    public Error Error { get; }

    /// <summary>
    /// Creates a successful result without a return value.
    /// </summary>
    /// <returns>A successful result.</returns>
    public static Result Success() => new(true, Error.None);

    /// <summary>
    /// Creates a failed result without a return value.
    /// </summary>
    /// <param name="error">The application error associated with the failure.</param>
    /// <returns>A failed result.</returns>
    public static Result Failure(Error error) => new(false, error);

    /// <summary>
    /// Creates a successful result with a return value.
    /// </summary>
    /// <typeparam name="TValue">The returned value type.</typeparam>
    /// <param name="value">The returned value.</param>
    /// <returns>A successful result with a value.</returns>
    public static Result<TValue> Success<TValue>(TValue value) =>
        new(value, true, Error.None);

    /// <summary>
    /// Creates a failed result with a return value type.
    /// </summary>
    /// <typeparam name="TValue">The returned value type.</typeparam>
    /// <param name="error">The application error associated with the failure.</param>
    /// <returns>A failed result for the requested value type.</returns>
    public static Result<TValue> Failure<TValue>(Error error) =>
        new(default, false, error);
}
