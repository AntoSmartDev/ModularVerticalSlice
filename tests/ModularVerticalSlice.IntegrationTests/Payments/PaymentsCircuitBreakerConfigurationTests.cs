using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.WebApi;
using Wolverine;

namespace ModularVerticalSlice.IntegrationTests.Payments;

/// <summary>
/// Verifies the host-level routing and durable listener circuit-breaker configuration.
/// </summary>
public class PaymentsCircuitBreakerConfigurationTests
{
    [Fact]
    public void ConfigureApplicationMessaging_Should_Accept_The_Payments_Queue_And_Breaker_Configuration()
    {
        var configuration = CreateConfiguration();
        var options = new WolverineOptions();

        var exception = Record.Exception(() => options.ConfigureApplicationMessaging(configuration));

        Assert.Null(exception);
    }

    [Fact]
    public void ConfigureApplicationMessaging_Should_Reject_Pause_Time_Not_Shorter_Than_Payment_Window()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Payments:CircuitBreaker:PauseTime"] = "00:05:00"
        });
        var options = new WolverineOptions();

        var exception = Assert.Throws<OptionsValidationException>(
            () => options.ConfigureApplicationMessaging(configuration));

        Assert.Contains(
            exception.Failures,
            failure => failure.Contains("shorter than Bookings:Lifecycle:PaymentWindow"));
    }

    private static IConfiguration CreateConfiguration(
        Dictionary<string, string?>? overrides = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Database"] = "Host=localhost;Database=test;Username=test;Password=test",
            ["Bookings:Lifecycle:PaymentWindow"] = "00:05:00",
            ["Payments:CircuitBreaker:MinimumThreshold"] = "5",
            ["Payments:CircuitBreaker:FailurePercentageThreshold"] = "60",
            ["Payments:CircuitBreaker:TrackingPeriod"] = "00:01:00",
            ["Payments:CircuitBreaker:PauseTime"] = "00:00:30"
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
                values[pair.Key] = pair.Value;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
