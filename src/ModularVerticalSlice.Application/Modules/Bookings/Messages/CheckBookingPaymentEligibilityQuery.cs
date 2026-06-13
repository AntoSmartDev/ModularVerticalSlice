namespace ModularVerticalSlice.Application.Modules.Bookings.Messages;

/// <summary>
/// Requests Bookings-owned confirmation that a booking still accepts payment.
/// </summary>
/// <param name="BookingId">The booking identifier to check.</param>
public sealed record CheckBookingPaymentEligibilityQuery(Guid BookingId);
