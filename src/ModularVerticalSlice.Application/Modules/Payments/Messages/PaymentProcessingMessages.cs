namespace ModularVerticalSlice.Application.Modules.Payments.Messages;

/// <summary>
/// Requests asynchronous payment processing for a booking.
/// </summary>
/// <param name="BookingId">The booking identifier that requires payment.</param>
/// <param name="EventId">The related catalog event identifier.</param>
/// <param name="UserId">The authenticated user that owns the booking.</param>
/// <param name="Quantity">The number of reserved tickets covered by the payment.</param>
/// <param name="PaymentDeadline">The immutable deadline after which payment processing is no longer allowed.</param>
public sealed record ProcessPaymentCommand(
    Guid BookingId,
    Guid EventId,
    string UserId,
    int Quantity,
    DateTimeOffset PaymentDeadline);

/// <summary>
/// Published when payment has completed successfully.
/// </summary>
/// <param name="BookingId">The related booking identifier.</param>
/// <param name="PaymentId">The payment identifier.</param>
/// <param name="PaidAt">The timestamp at which the payment completed.</param>
public sealed record PaymentSucceededEvent(
    Guid BookingId,
    Guid PaymentId,
    DateTimeOffset PaidAt);

/// <summary>
/// Published when payment has failed as a business outcome.
/// </summary>
/// <param name="BookingId">The related booking identifier.</param>
/// <param name="PaymentId">The payment identifier.</param>
/// <param name="Reason">The business reason of the payment failure.</param>
/// <param name="FailedAt">The timestamp at which the payment failed.</param>
public sealed record PaymentFailedEvent(
    Guid BookingId,
    Guid PaymentId,
    string Reason,
    DateTimeOffset FailedAt);
