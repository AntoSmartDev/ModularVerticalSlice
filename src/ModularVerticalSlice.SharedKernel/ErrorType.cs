namespace ModularVerticalSlice.SharedKernel;

/// <summary>
/// Categorizes application errors so they can be mapped consistently
/// to HTTP responses, logs and observability signals.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// No error is present.
    /// </summary>
    None = 0,

    /// <summary>
    /// A generic application failure occurred.
    /// </summary>
    Failure = 1,

    /// <summary>
    /// The requested resource could not be found.
    /// </summary>
    NotFound = 2,

    /// <summary>
    /// The request violates one or more validation rules.
    /// </summary>
    Validation = 3,

    /// <summary>
    /// The request conflicts with the current state.
    /// </summary>
    Conflict = 4,

    /// <summary>
    /// The caller is not authenticated.
    /// </summary>
    Unauthorized = 5,

    /// <summary>
    /// The caller is authenticated but not allowed to perform the action.
    /// </summary>
    Forbidden = 6
}
