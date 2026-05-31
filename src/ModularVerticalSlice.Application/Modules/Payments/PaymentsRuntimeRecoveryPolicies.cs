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
    private static readonly TimeSpan[] RuntimeRecoveryCooldowns =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(15)
    ];

    /// <summary>
    /// Applies the baseline runtime-recovery handling for Payments technical failures.
    /// </summary>
    public static void Configure(WolverineOptions options)
    {
        options.Policies
            .OnException<PaymentTechnicalFailureException>(
                x => x.RecoveryDecision == PaymentRecoveryDecisionKind.RuntimeManagedRecovery,
                "Payments runtime-managed recovery")
            .RetryWithCooldown(RuntimeRecoveryCooldowns);

        options.Policies
            .OnException<PaymentTechnicalFailureException>(
                x => x.RecoveryDecision == PaymentRecoveryDecisionKind.EscalateOrManualIntervention,
                "Payments escalation or manual intervention")
            .MoveToErrorQueue();
    }
}
