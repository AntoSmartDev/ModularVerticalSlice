using ModularVerticalSlice.Application.Modules.Payments.Domain;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using ModularVerticalSlice.Application.Modules.Payments;
using Wolverine;
using System.Linq;

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

    /// <summary>
    /// Verifies that the Payments runtime-recovery baseline registers both technical-failure branches.
    /// </summary>
    [Fact]
    public void ConfigurePaymentsRuntimeRecovery_Should_Register_Both_Runtime_Recovery_Branches()
    {
        var options = new WolverineOptions();
        var baselineCount = options.Policies.Failures.Count();

        PaymentsRuntimeRecoveryPolicies.Configure(options);

        Assert.Equal(baselineCount + 2, options.Policies.Failures.Count());
    }

    /// <summary>
    /// Verifies that the runtime-managed recovery branch is observable as an explicit runtime-retry route.
    /// </summary>
    [Fact]
    public void DescribeRuntimeObservability_Should_Map_Runtime_Managed_Recovery_To_Runtime_Retry()
    {
        var exception = PaymentTechnicalFailureException.RuntimeManagedRecovery("temporary");

        var route = PaymentsTechnicalFailureRuntimeObservability.Describe(exception);

        Assert.Equal(PaymentsTechnicalFailureRuntimeObservability.RuntimeManagedRecoveryPolicyName, route.PolicyName);
        Assert.Equal(PaymentsTechnicalFailureRuntimeObservability.RuntimeRetryRoute, route.RouteName);
        Assert.True(route.UsesRuntimeRetry);
        Assert.False(route.UsesErrorQueue);
    }

    /// <summary>
    /// Verifies that the escalation branch is observable as an explicit error-queue route.
    /// </summary>
    [Fact]
    public void DescribeRuntimeObservability_Should_Map_Escalation_To_Error_Queue()
    {
        var exception = PaymentTechnicalFailureException.EscalateOrManualIntervention("terminal");

        var route = PaymentsTechnicalFailureRuntimeObservability.Describe(exception);

        Assert.Equal(PaymentsTechnicalFailureRuntimeObservability.EscalationToDlqPolicyName, route.PolicyName);
        Assert.Equal(PaymentsTechnicalFailureRuntimeObservability.ErrorQueueRoute, route.RouteName);
        Assert.False(route.UsesRuntimeRetry);
        Assert.True(route.UsesErrorQueue);
    }
}
