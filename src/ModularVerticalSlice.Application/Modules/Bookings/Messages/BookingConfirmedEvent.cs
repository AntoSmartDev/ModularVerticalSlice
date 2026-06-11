namespace ModularVerticalSlice.Application.Modules.Bookings.Messages;

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
