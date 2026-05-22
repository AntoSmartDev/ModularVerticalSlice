using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Catalog.Domain;

/// <summary>
/// Protects ticket availability invariants for Catalog events.
/// </summary>
public static class TicketReservationPolicy
{
    /// <summary>
    /// Verifies whether the requested quantity can be reserved from the current availability.
    /// </summary>
    /// <param name="availableTickets">The number of tickets currently available.</param>
    /// <param name="requestedQuantity">The number of tickets being requested.</param>
    /// <returns>
    /// A successful result when the reservation is allowed; otherwise a validation or conflict failure.
    /// </returns>
    public static Result CanReserve(int availableTickets, int requestedQuantity)
    {
        if (requestedQuantity <= 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidQuantity",
                    "The requested ticket quantity must be greater than zero."));
        }

        if (availableTickets < 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidAvailability",
                    "The available ticket count cannot be negative."));
        }

        if (requestedQuantity > availableTickets)
        {
            return Result.Failure(
                Error.Conflict(
                    "Catalog.NotEnoughTickets",
                    "Not enough tickets are available for the requested reservation."));
        }

        return Result.Success();
    }
}
