namespace ModularVerticalSlice.Application.Modules.Bookings.Messages;

/// <summary>
/// Raised when the payment window of a booking has expired.
/// </summary>
/// <remarks>
/// The message may be scheduled by infrastructure, but its business meaning is
/// that the booking payment window is over.
/// </remarks>
/// <param name="BookingId">The affected booking identifier.</param>
/// <param name="ExpiredAt">The instant when the payment window expired.</param>
public sealed record BookingPaymentTimeoutExpiredEvent(
    Guid BookingId,
    DateTimeOffset ExpiredAt);
