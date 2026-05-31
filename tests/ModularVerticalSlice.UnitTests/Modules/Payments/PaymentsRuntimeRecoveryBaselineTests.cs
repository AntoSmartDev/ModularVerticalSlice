using ModularVerticalSlice.Application.Modules.Payments.Domain;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using ModularVerticalSlice.Application.Modules.Payments;
using Wolverine;

namespace ModularVerticalSlice.UnitTests.Modules.Payments;

/// <summary>
/// Verifies the explicit runtime-recovery ownership baseline for Payments technical failures.
/// </summary>
public class PaymentsRuntimeRecoveryBaselineTests
{
    /// <summary>
    /// Verifies that the runtime-managed recovery branch stays explicit on the technical-failure contract.
    /// </summary>
    [Fact]
    public void PaymentTechnicalFailureException_RuntimeManagedRecovery_Should_Stay_RuntimeOwned()
    {
        var exception = PaymentTechnicalFailureException.RuntimeManagedRecovery("temporary");

        Assert.True(exception.IsRetriable);
        Assert.Equal(PaymentRecoveryDecisionKind.RuntimeManagedRecovery, exception.RecoveryDecision);
    }

    /// <summary>
    /// Verifies that the escalation branch stays explicit on the technical-failure contract.
    /// </summary>
    [Fact]
    public void PaymentTechnicalFailureException_Escalation_Should_Stay_RuntimeOwned()
    {
        var exception = PaymentTechnicalFailureException.EscalateOrManualIntervention("terminal");

        Assert.False(exception.IsRetriable);
        Assert.Equal(PaymentRecoveryDecisionKind.EscalateOrManualIntervention, exception.RecoveryDecision);
    }

    /// <summary>
    /// Verifies that the host messaging baseline can apply the Payments runtime-recovery policy without error.
    /// </summary>
    [Fact]
    public void ConfigurePaymentsRuntimeRecovery_Should_Apply_Payments_Technical_Failure_Policies()
    {
        var options = new WolverineOptions();

        PaymentsRuntimeRecoveryPolicies.Configure(options);

        Assert.NotEmpty(options.Policies.Failures);
    }
}
