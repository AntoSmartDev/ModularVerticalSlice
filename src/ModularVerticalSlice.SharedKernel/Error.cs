namespace ModularVerticalSlice.SharedKernel;

/// <summary>
/// Represents a typed application error.
/// </summary>
/// <param name="Code">Stable machine-readable error code.</param>
/// <param name="Description">Human-readable error description.</param>
/// <param name="Type">Error category used for mapping to HTTP and observability.</param>
/// <param name="ValidationErrors">Optional validation details grouped by field.</param>
public sealed record Error(
    string Code,
    string Description,
    ErrorType Type,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null)
{
    /// <summary>
    /// Represents the absence of an error.
    /// </summary>
    public static readonly Error None = new(
        string.Empty,
        string.Empty,
        ErrorType.None);

    /// <summary>
    /// Creates a generic failure error.
    /// </summary>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="description">Human-readable error description.</param>
    /// <returns>A failure error.</returns>
    public static Error Failure(string code, string description) =>
        new(code, description, ErrorType.Failure);

    /// <summary>
    /// Creates a not-found error.
    /// </summary>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="description">Human-readable error description.</param>
    /// <returns>A not-found error.</returns>
    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    /// <summary>
    /// Creates a validation error.
    /// </summary>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="description">Human-readable error description.</param>
    /// <param name="validationErrors">Optional validation details grouped by field.</param>
    /// <returns>A validation error.</returns>
    public static Error Validation(
        string code,
        string description,
        IReadOnlyDictionary<string, string[]>? validationErrors = null) =>
        new(code, description, ErrorType.Validation, validationErrors);

    /// <summary>
    /// Creates a conflict error.
    /// </summary>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="description">Human-readable error description.</param>
    /// <returns>A conflict error.</returns>
    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    /// <summary>
    /// Creates an unauthorized error.
    /// </summary>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="description">Human-readable error description.</param>
    /// <returns>An unauthorized error.</returns>
    public static Error Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    /// <summary>
    /// Creates a forbidden error.
    /// </summary>
    /// <param name="code">Stable machine-readable error code.</param>
    /// <param name="description">Human-readable error description.</param>
    /// <returns>A forbidden error.</returns>
    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);
}
