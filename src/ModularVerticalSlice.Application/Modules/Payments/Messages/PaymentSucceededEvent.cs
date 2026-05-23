namespace ModularVerticalSlice.Application.Modules.Payments.Messages;

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
