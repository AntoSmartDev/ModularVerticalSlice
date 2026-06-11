using ModularVerticalSlice.Application.Modules.Notifications.Features.BookingConfirmation;
using Wolverine;
using Wolverine.ErrorHandling;

namespace ModularVerticalSlice.Application.Modules.Notifications;

/// <summary>
/// Configures Wolverine-owned recovery for notification delivery failures.
/// </summary>
public static class NotificationsRuntimeRecoveryPolicies
{
    /// <summary>
    /// Configures transient retry and permanent-failure routing.
    /// </summary>
    public static void Configure(WolverineOptions options)
    {
        options.Policies
            .OnException<NotificationDeliveryException>(
                exception => exception.IsTransient,
                "Notifications transient delivery failure")
            .RetryWithCooldown(TimeSpan.FromMilliseconds(100));

        options.Policies
            .OnException<NotificationDeliveryException>(
                exception => !exception.IsTransient,
                "Notifications permanent delivery failure")
            .MoveToErrorQueue();
    }
}
