using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.Events;

/// <summary>
/// Contains validation rules for the Catalog events feature baseline.
/// </summary>
internal static class EventValidators
{
    /// <summary>
    /// Validates the baseline create-event command contract.
    /// </summary>
    /// <param name="command">The command to validate.</param>
    /// <returns>
    /// A successful result when the command is valid; otherwise a validation failure.
    /// </returns>
    public static Result Validate(CreateEventCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidEventTitle",
                    "The event title is required."));
        }

        if (command.TicketPrice <= 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidTicketPrice",
                    "The ticket price must be greater than zero."));
        }

        if (command.AvailableTickets < 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidAvailableTickets",
                    "The available ticket count cannot be negative."));
        }

        return Result.Success();
    }
}
