namespace ModularVerticalSlice.Application.Modules.Bookings.Messages;

/// <summary>
/// Published when a booking has been created and tickets have been reserved.
/// </summary>
/// <remarks>
/// This event is the entry point for the booking lifecycle saga and
/// represents the first durable handoff after immediate booking creation.
/// </remarks>
/// <param name="BookingId">The created booking identifier.</param>
/// <param name="EventId">The catalog event identifier tied to the booking.</param>
/// <param name="UserId">The authenticated user that owns the booking.</param>
/// <param name="Quantity">The number of reserved tickets.</param>
/// <param name="CreatedAt">The creation timestamp of the booking.</param>
public sealed record BookingCreatedEvent(
    Guid BookingId,
    Guid EventId,
    string UserId,
    int Quantity,
    DateTimeOffset CreatedAt);

/// <summary>
/// Published when a pending booking has been successfully confirmed.
/// </summary>
/// <param name="BookingId">The confirmed booking identifier.</param>
/// <param name="EventId">The catalog event identifier tied to the booking.</param>
/// <param name="UserId">The user that owns the booking.</param>
/// <param name="ConfirmedAt">The confirmation timestamp.</param>
public sealed record BookingConfirmedEvent(
    Guid BookingId,
    Guid EventId,
    string UserId,
    DateTimeOffset ConfirmedAt);

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
