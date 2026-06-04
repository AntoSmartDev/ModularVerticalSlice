using ModularVerticalSlice.Application.Modules.Payments.Domain;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using Wolverine;
using Wolverine.ErrorHandling;

namespace ModularVerticalSlice.Application.Modules.Payments;

/// <summary>
/// Holds the explicit Wolverine-style runtime-recovery baseline for Payments technical failures.
/// </summary>
public static class PaymentsRuntimeRecoveryPolicies
{
    /// <summary>
    /// Applies the baseline runtime-recovery handling for Payments technical failures.
    /// </summary>
    public static void Configure(WolverineOptions options)
    {
        // Payments keeps technical-failure ownership in Wolverine policies.
        // This baseline makes retry vs DLQ routing explicit without adding local retry loops.
        options.Policies
            .OnException<PaymentTechnicalFailureException>(
                x => x.RecoveryDecision == PaymentRecoveryDecisionKind.RuntimeManagedRecovery,
                PaymentsTechnicalFailureRuntimeObservability.RuntimeManagedRecoveryPolicyName)
            .RetryWithCooldown(PaymentsTechnicalFailureRuntimeObservability.RuntimeRecoveryCooldowns.ToArray());

        options.Policies
            .OnException<PaymentTechnicalFailureException>(
                x => x.RecoveryDecision == PaymentRecoveryDecisionKind.EscalateOrManualIntervention,
                PaymentsTechnicalFailureRuntimeObservability.EscalationToDlqPolicyName)
            .MoveToErrorQueue();
    }
}
