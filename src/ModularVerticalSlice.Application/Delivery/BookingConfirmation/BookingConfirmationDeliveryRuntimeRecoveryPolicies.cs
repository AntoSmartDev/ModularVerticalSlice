using Wolverine;
using Wolverine.ErrorHandling;

namespace ModularVerticalSlice.Application.Delivery.BookingConfirmation;

/// <summary>
/// Configures Wolverine-owned recovery for booking-confirmation delivery
/// failures.
/// </summary>
public static class BookingConfirmationDeliveryRuntimeRecoveryPolicies
{
    /// <summary>
    /// Configures transient retry and permanent-failure routing.
    /// </summary>
    public static void Configure(WolverineOptions options)
    {
        options.Policies
            .OnException<NotificationDeliveryException>(
                exception => exception.IsTransient,
                "Booking confirmation transient delivery failure")
            .RetryWithCooldown(TimeSpan.FromMilliseconds(100));

        options.Policies
            .OnException<NotificationDeliveryException>(
                exception => !exception.IsTransient,
                "Booking confirmation permanent delivery failure")
            .MoveToErrorQueue();
    }
}
