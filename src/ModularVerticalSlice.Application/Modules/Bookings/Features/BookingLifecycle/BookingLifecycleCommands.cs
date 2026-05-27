namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Confirms an existing pending booking after successful payment.
/// </summary>
/// <param name="BookingId">The target booking identifier.</param>
public sealed record ConfirmBookingCommand(Guid BookingId);

/// <summary>
/// Cancels an existing booking after a failure or business decision.
/// </summary>
/// <param name="BookingId">The target booking identifier.</param>
public sealed record CancelBookingCommand(Guid BookingId);

/// <summary>
/// Expires an existing pending booking after the payment window elapses.
/// </summary>
/// <param name="BookingId">The target booking identifier.</param>
public sealed record ExpireBookingCommand(Guid BookingId);
