namespace ModularVerticalSlice.Application.Delivery.BookingConfirmation;

/// <summary>
/// Represents a technical booking-confirmation delivery failure owned by
/// runtime recovery.
/// </summary>
public sealed class NotificationDeliveryException(string message, bool isTransient) : Exception(message)
{
    /// <summary>
    /// Indicates whether Wolverine should retry the failed delivery.
    /// </summary>
    public bool IsTransient { get; } = isTransient;

    /// <summary>
    /// Creates a transient delivery failure.
    /// </summary>
    public static NotificationDeliveryException Transient() =>
        new("The fake email provider is temporarily unavailable.", true);

    /// <summary>
    /// Creates a permanent delivery failure.
    /// </summary>
    public static NotificationDeliveryException Permanent() =>
        new("The fake email provider permanently rejected the email.", false);
}
