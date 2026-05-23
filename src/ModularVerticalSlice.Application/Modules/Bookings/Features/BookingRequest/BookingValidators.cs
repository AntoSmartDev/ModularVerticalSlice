using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingRequest;

/// <summary>
/// Contains validation rules for the Bookings request baseline.
/// </summary>
internal static class BookingValidators
{
    /// <summary>
    /// Validates the baseline booking request command contract.
    /// </summary>
    /// <param name="command">The command to validate.</param>
    /// <returns>
    /// A successful result when the command is valid; otherwise a validation failure.
    /// </returns>
    public static Result Validate(RequestBookingCommand command)
    {
        if (command.EventId == Guid.Empty)
        {
            return Result.Failure(
                Error.Validation(
                    "Bookings.InvalidEventId",
                    "The event identifier is required."));
        }

        if (command.Quantity <= 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Bookings.InvalidQuantity",
                    "The booking quantity must be greater than zero."));
        }

        if (command.ClientRequestId == Guid.Empty)
        {
            return Result.Failure(
                Error.Validation(
                    "Bookings.InvalidClientRequestId",
                    "The client request identifier is required."));
        }

        return Result.Success();
    }
}
