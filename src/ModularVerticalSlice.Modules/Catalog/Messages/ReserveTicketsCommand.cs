namespace ModularVerticalSlice.Modules.Catalog.Messages;

/// <summary>
/// Requests the reservation of tickets for a specific booking.
/// </summary>
/// <remarks>
/// This command is invoked synchronously by the Bookings module to guarantee immediate consistency.
/// </remarks>
/// <param name="EventId">The event identifier.</param>
/// <param name="Quantity">The number of tickets to reserve.</param>
/// <param name="BookingId">The booking identifier that owns the reservation.</param>
public sealed record ReserveTicketsCommand(
    Guid EventId,
    int Quantity,
    Guid BookingId);
