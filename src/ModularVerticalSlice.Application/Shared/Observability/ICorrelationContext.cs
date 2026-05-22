namespace ModularVerticalSlice.Application.Shared.Observability;

/// <summary>
/// Provides access to the current correlation identifier for module-level flows.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>
    /// Gets the current correlation identifier.
    /// </summary>
    string CorrelationId { get; }
}
