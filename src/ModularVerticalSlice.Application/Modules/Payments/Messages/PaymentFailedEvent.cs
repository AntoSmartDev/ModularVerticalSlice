namespace ModularVerticalSlice.Application.Modules.Payments.Messages;

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
