namespace ModularVerticalSlice.SharedKernel;

/// <summary>
/// Represents the outcome of an operation that returns a value.
/// </summary>
/// <typeparam name="TValue">The returned value type.</typeparam>
public class Result<TValue> : Result
{
    private readonly TValue? _value;

    /// <summary>
    /// Initializes a new instance of the <see cref="Result{TValue}" /> class.
    /// </summary>
    /// <param name="value">The returned value.</param>
    /// <param name="isSuccess">Indicates whether the operation succeeded.</param>
    /// <param name="error">The application error associated with the result.</param>
    protected internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// Gets the returned value when the operation succeeds.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when attempting to access the value of a failed result.
    /// </exception>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException(
            "Cannot access the value of a failed result.");

    /// <summary>
    /// Converts a value into a successful result.
    /// </summary>
    /// <param name="value">The returned value.</param>
    public static implicit operator Result<TValue>(TValue value) =>
        Success(value);

    /// <summary>
    /// Converts an error into a failed result.
    /// </summary>
    /// <param name="error">The application error.</param>
    public static implicit operator Result<TValue>(Error error) =>
        Failure<TValue>(error);
}
