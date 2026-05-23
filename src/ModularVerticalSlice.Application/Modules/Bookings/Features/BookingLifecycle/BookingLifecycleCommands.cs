namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Creates a new booking for a catalog event.
/// </summary>
/// <param name="EventId">The target event identifier.</param>
/// <param name="Quantity">The number of tickets requested for the booking.</param>
/// <param name="ClientRequestId">The client-side idempotency identifier.</param>
public record CreateBookingCommand(
    Guid EventId,
    int Quantity,
    Guid ClientRequestId);

/// <summary>
/// Transitional alias kept to avoid breaking the current baseline while the
/// Bookings flow is being realigned to the coordinated CreateBooking use case.
/// </summary>
/// <param name="EventId">The target event identifier.</param>
/// <param name="Quantity">The number of tickets requested for the booking.</param>
/// <param name="ClientRequestId">The client-side idempotency identifier.</param>
public sealed record RequestBookingCommand(
    Guid EventId,
    int Quantity,
    Guid ClientRequestId)
    : CreateBookingCommand(EventId, Quantity, ClientRequestId);

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
