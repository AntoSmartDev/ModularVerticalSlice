using Microsoft.Extensions.Logging;
using Wolverine;

namespace ModularVerticalSlice.Application.Shared.Observability;

/// <summary>
/// Pushes the Wolverine correlation id into the logging scope for every handled
/// message, so handler logs in a chain share one correlation id.
/// </summary>
/// <remarks>
/// Wolverine already derives the correlation id from the ambient
/// <see cref="System.Diagnostics.Activity"/> and propagates it across a message
/// chain; it simply does not add it to the <see cref="ILogger"/> scope. This
/// middleware closes that gap and nothing more. Registered globally via
/// <c>WolverineOptions.Policies.AddMiddleware</c>.
/// </remarks>
public static class CorrelationLoggingMiddleware
{
    /// <summary>Logging scope key carrying the correlation id.</summary>
    public const string CorrelationIdKey = "CorrelationId";

    /// <summary>
    /// Opens a logging scope carrying the envelope correlation id before the
    /// handler runs. The returned value is disposed by <see cref="Finally"/>.
    /// </summary>
    public static CorrelationLogScope Before(Envelope envelope, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(logger);

        if (string.IsNullOrEmpty(envelope.CorrelationId))
        {
            return default;
        }

        var scope = logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationIdKey] = envelope.CorrelationId
        });

        return new CorrelationLogScope(scope);
    }

    /// <summary>Closes the correlation logging scope after the handler completes.</summary>
    public static void Finally(CorrelationLogScope scope) => scope.Dispose();
}

/// <summary>
/// Concrete, non-null carrier for the optional correlation logging scope so the
/// Wolverine code generation can thread it from <c>Before</c> to <c>Finally</c>.
/// </summary>
public readonly struct CorrelationLogScope(IDisposable? scope) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => scope?.Dispose();
}
