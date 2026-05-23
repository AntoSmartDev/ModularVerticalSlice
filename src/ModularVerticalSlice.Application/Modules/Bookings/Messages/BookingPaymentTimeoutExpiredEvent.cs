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
/// <param name="EventId">The related catalog event identifier.</param>
/// <param name="Quantity">The reserved ticket quantity to release if the booking expires.</param>
public sealed record BookingPaymentTimeoutExpiredEvent(
    Guid BookingId,
    DateTimeOffset ExpiredAt,
    Guid EventId = default,
    int Quantity = 0);
