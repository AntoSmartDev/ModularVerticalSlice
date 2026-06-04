using ModularVerticalSlice.Application.Modules.Payments.Domain;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

namespace ModularVerticalSlice.Application.Modules.Payments;

/// <summary>
/// Holds the explicit runtime-observability baseline for Payments technical failures.
/// This stays intentionally local and lightweight: the goal is to make retry vs DLQ
/// intent reviewable without introducing provider-specific infrastructure.
/// </summary>
public static class PaymentsTechnicalFailureRuntimeObservability
{
    /// <summary>
    /// Wolverine policy label for the runtime-managed recovery branch.
    /// </summary>
    public const string RuntimeManagedRecoveryPolicyName =
        "Payments technical failure runtime-managed recovery";

    /// <summary>
    /// Wolverine policy label for the escalation-to-DLQ branch.
    /// </summary>
    public const string EscalationToDlqPolicyName =
        "Payments technical failure escalation to DLQ";

    /// <summary>
    /// Review-friendly route name for runtime retry handling.
    /// </summary>
    public const string RuntimeRetryRoute = "RuntimeRetry";

    /// <summary>
    /// Review-friendly route name for error-queue escalation.
    /// </summary>
    public const string ErrorQueueRoute = "ErrorQueue";

    /// <summary>
    /// Describes how the current Payments runtime baseline will expose a technical failure.
    /// </summary>
    public static PaymentsTechnicalFailureRuntimeRoute Describe(
        PaymentTechnicalFailureException exception)
    {
        return exception.RecoveryDecision switch
        {
            PaymentRecoveryDecisionKind.RuntimeManagedRecovery => new PaymentsTechnicalFailureRuntimeRoute(
                RuntimeManagedRecoveryPolicyName,
                RuntimeRetryRoute,
                UsesRuntimeRetry: true,
                UsesErrorQueue: false),

            PaymentRecoveryDecisionKind.EscalateOrManualIntervention => new PaymentsTechnicalFailureRuntimeRoute(
                EscalationToDlqPolicyName,
                ErrorQueueRoute,
                UsesRuntimeRetry: false,
                UsesErrorQueue: true),

            _ => throw new InvalidOperationException(
                $"Unsupported Payments recovery decision '{exception.RecoveryDecision}'.")
        };
    }
}

/// <summary>
/// Review-friendly shape for the current Payments runtime observability baseline.
/// </summary>
public sealed record PaymentsTechnicalFailureRuntimeRoute(
    string PolicyName,
    string RouteName,
    bool UsesRuntimeRetry,
    bool UsesErrorQueue);
