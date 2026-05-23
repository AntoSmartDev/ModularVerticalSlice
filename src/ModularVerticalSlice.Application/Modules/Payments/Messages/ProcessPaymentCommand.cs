namespace ModularVerticalSlice.Application.Modules.Payments.Messages;

/// <summary>
/// Requests asynchronous payment processing for a booking.
/// </summary>
/// <param name="BookingId">The booking identifier that requires payment.</param>
/// <param name="EventId">The related catalog event identifier.</param>
/// <param name="UserId">The authenticated user that owns the booking.</param>
/// <param name="Quantity">The number of reserved tickets covered by the payment.</param>
public sealed record ProcessPaymentCommand(
    Guid BookingId,
    Guid EventId,
    string UserId,
    int Quantity);
