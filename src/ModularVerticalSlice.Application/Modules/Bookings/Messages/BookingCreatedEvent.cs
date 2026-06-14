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
