using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

namespace ModularVerticalSlice.UnitTests.Modules.Payments;

/// <summary>
/// Verifies the Payments listener circuit-breaker configuration contract.
/// </summary>
public class PaymentsCircuitBreakerOptionsTests
{
    [Fact]
    public void Validate_Should_Accept_The_Default_Configuration()
    {
        var options = new PaymentProcessingCircuitBreakerOptions();

        var result = PaymentProcessingCircuitBreakerOptions.Validate(options, TimeSpan.FromMinutes(5));

        Assert.True(result.Succeeded);
        Assert.Equal(5, options.MinimumThreshold);
        Assert.Equal(60, options.FailurePercentageThreshold);
        Assert.Equal(TimeSpan.FromMinutes(1), options.TrackingPeriod);
        Assert.Equal(TimeSpan.FromSeconds(30), options.PauseTime);
    }

    [Fact]
    public void Validate_Should_Reject_Invalid_Local_Values()
    {
        var options = new PaymentProcessingCircuitBreakerOptions
        {
            MinimumThreshold = 0,
            FailurePercentageThreshold = 101,
            TrackingPeriod = TimeSpan.Zero,
            PauseTime = TimeSpan.Zero
        };

        var result = PaymentProcessingCircuitBreakerOptions.Validate(options);

        Assert.True(result.Failed);
        Assert.Equal(4, result.Failures.Count());
    }

    [Fact]
    public void Validate_Should_Require_Pause_Time_Shorter_Than_Payment_Window()
    {
        var options = new PaymentProcessingCircuitBreakerOptions
        {
            PauseTime = TimeSpan.FromMinutes(5)
        };

        var result = PaymentProcessingCircuitBreakerOptions.Validate(options, TimeSpan.FromMinutes(5));

        Assert.True(result.Failed);
        Assert.Contains(
            result.Failures,
            failure => failure.Contains("shorter than Bookings:Lifecycle:PaymentWindow"));
    }

    [Fact]
    public void ShouldAffectCircuitBreaker_Should_Include_Only_Degraded_Recoverable_Failures()
    {
        Assert.True(PaymentProcessingRuntimeRecoveryPolicies.ShouldAffectCircuitBreaker(
            PaymentTechnicalFailureException.DegradedRecoverable("temporary")));
        Assert.False(PaymentProcessingRuntimeRecoveryPolicies.ShouldAffectCircuitBreaker(
            PaymentTechnicalFailureException.Terminal("terminal")));
        Assert.False(PaymentProcessingRuntimeRecoveryPolicies.ShouldAffectCircuitBreaker(
            PaymentTechnicalFailureException.EscalateOrManualIntervention("terminal")));
    }
}
