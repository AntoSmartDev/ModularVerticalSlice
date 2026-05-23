namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingRequest;

/// <summary>
/// Requests the creation of a new booking for a catalog event.
/// </summary>
/// <param name="EventId">The target event identifier.</param>
/// <param name="Quantity">The number of tickets requested for the booking.</param>
/// <param name="ClientRequestId">The client-side idempotency identifier.</param>
public sealed record RequestBookingCommand(
    Guid EventId,
    int Quantity,
    Guid ClientRequestId);
