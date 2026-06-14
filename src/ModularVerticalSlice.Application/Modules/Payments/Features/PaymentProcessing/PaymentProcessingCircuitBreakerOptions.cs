using Microsoft.Extensions.Options;

namespace ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

/// <summary>
/// Configures the listener circuit breaker for the durable Payments processing queue.
/// </summary>
public sealed class PaymentProcessingCircuitBreakerOptions
{
    /// <summary>
    /// Gets the configuration section used by the Payments circuit breaker.
    /// </summary>
    public const string SectionName = "Payments:CircuitBreaker";

    /// <summary>
    /// Gets the dedicated local queue used by <c>ProcessPaymentCommand</c>.
    /// </summary>
    public const string QueueName = "payments-processing";

    /// <summary>
    /// Gets or sets the minimum number of processed messages required before evaluating failures.
    /// </summary>
    public int MinimumThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the failure percentage that pauses the Payments queue.
    /// </summary>
    public int FailurePercentageThreshold { get; set; } = 60;

    /// <summary>
    /// Gets or sets how long processing outcomes contribute to the breaker decision.
    /// </summary>
    public TimeSpan TrackingPeriod { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets how long the Payments queue remains paused after the breaker trips.
    /// </summary>
    public TimeSpan PauseTime { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Validates the breaker values independently from other module settings.
    /// </summary>
    public static ValidateOptionsResult Validate(PaymentProcessingCircuitBreakerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.MinimumThreshold <= 0)
            failures.Add($"{nameof(MinimumThreshold)} must be greater than zero.");

        if (options.FailurePercentageThreshold is < 1 or > 100)
            failures.Add($"{nameof(FailurePercentageThreshold)} must be between 1 and 100.");

        if (options.TrackingPeriod <= TimeSpan.Zero)
            failures.Add($"{nameof(TrackingPeriod)} must be greater than zero.");

        if (options.PauseTime <= TimeSpan.Zero)
            failures.Add($"{nameof(PauseTime)} must be greater than zero.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    /// <summary>
    /// Validates the breaker values and their relationship with the booking payment window.
    /// </summary>
    public static ValidateOptionsResult Validate(
        PaymentProcessingCircuitBreakerOptions options,
        TimeSpan paymentWindow)
    {
        var localValidation = Validate(options);
        var failures = localValidation.Failed
            ? localValidation.Failures.ToList()
            : [];

        if (paymentWindow <= TimeSpan.Zero)
            failures.Add("Bookings:Lifecycle:PaymentWindow must be greater than zero.");
        else if (options.PauseTime >= paymentWindow)
            failures.Add($"{nameof(PauseTime)} must be shorter than Bookings:Lifecycle:PaymentWindow.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
