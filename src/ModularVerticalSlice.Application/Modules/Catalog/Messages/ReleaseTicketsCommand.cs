namespace ModularVerticalSlice.Modules.Catalog.Messages;

/// <summary>
/// Requests the release of previously reserved tickets for a booking.
/// </summary>
/// <remarks>
/// This command is used as a compensation action when payment fails or expires.
/// </remarks>
/// <param name="EventId">The event identifier.</param>
/// <param name="Quantity">The number of tickets to release.</param>
/// <param name="BookingId">The booking identifier that owns the reservation.</param>
public sealed record ReleaseTicketsCommand(
    Guid EventId,
    int Quantity,
    Guid BookingId);
