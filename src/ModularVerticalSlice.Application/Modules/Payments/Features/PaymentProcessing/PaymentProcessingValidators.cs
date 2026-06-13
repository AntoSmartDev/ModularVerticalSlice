using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

/// <summary>
/// Validates the baseline ProcessPayment command locally inside the feature.
/// </summary>
public static class PaymentProcessingValidators
{
    /// <summary>
    /// Validates the baseline payment-processing command shape.
    /// </summary>
    public static Result Validate(ProcessPaymentCommand command)
    {
        if (command.BookingId == Guid.Empty)
        {
            return Result.Failure(
                Error.Validation(
                    "Payments.InvalidBookingId",
                    "A valid booking identifier is required."));
        }

        if (command.EventId == Guid.Empty)
        {
            return Result.Failure(
                Error.Validation(
                    "Payments.InvalidEventId",
                    "A valid event identifier is required."));
        }

        if (command.Quantity <= 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Payments.InvalidQuantity",
                    "The payment quantity must be greater than zero."));
        }

        if (string.IsNullOrWhiteSpace(command.UserId))
        {
            return Result.Failure(
                Error.Validation(
                    "Payments.InvalidUserId",
                    "A payment owner is required."));
        }

        if (command.PaymentDeadline == default)
        {
            return Result.Failure(
                Error.Validation(
                    "Payments.InvalidPaymentDeadline",
                    "A valid payment deadline is required."));
        }

        return Result.Success();
    }
}
