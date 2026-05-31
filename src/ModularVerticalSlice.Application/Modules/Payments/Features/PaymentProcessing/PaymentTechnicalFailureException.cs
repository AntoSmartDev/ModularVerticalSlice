namespace ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

/// <summary>
/// Raised when the payment gateway reports a technical failure that should be
/// delegated to runtime retry semantics.
/// </summary>
public sealed class PaymentTechnicalFailureException(
    string message,
    bool isRetriable,
    Domain.PaymentProviderStateKind providerState,
    Domain.PaymentRecoveryDecisionKind recoveryDecision) : Exception(message)
{
    /// <summary>
    /// Indicates whether the current technical failure is considered retriable by the baseline shaping.
    /// </summary>
    public bool IsRetriable { get; } = isRetriable;

    /// <summary>
    /// Describes the local provider-state semantics inferred from the technical failure.
    /// </summary>
    public Domain.PaymentProviderStateKind ProviderState { get; } = providerState;

    /// <summary>
    /// Describes the local recovery decision suggested after the technical failure.
    /// </summary>
    public Domain.PaymentRecoveryDecisionKind RecoveryDecision { get; } = recoveryDecision;

    /// <summary>
    /// Creates a technical-failure exception for a degraded-but-recoverable provider state.
    /// </summary>
    public static PaymentTechnicalFailureException DegradedRecoverable(string message) =>
        new(
            message,
            true,
            Domain.PaymentProviderStateKind.DegradedRecoverable,
            Domain.PaymentRecoveryDecisionKind.RuntimeManagedRecovery);

    /// <summary>
    /// Creates a technical-failure exception for a terminal provider state.
    /// </summary>
    public static PaymentTechnicalFailureException Terminal(string message) =>
        new(
            message,
            false,
            Domain.PaymentProviderStateKind.Terminal,
            Domain.PaymentRecoveryDecisionKind.EscalateOrManualIntervention);

    /// <summary>
    /// Creates a retriable technical-failure exception.
    /// </summary>
    public static PaymentTechnicalFailureException Retriable(string message) =>
        DegradedRecoverable(message);

    /// <summary>
    /// Creates a non-retriable technical-failure exception.
    /// </summary>
    public static PaymentTechnicalFailureException NonRetriable(string message) =>
        Terminal(message);
}
