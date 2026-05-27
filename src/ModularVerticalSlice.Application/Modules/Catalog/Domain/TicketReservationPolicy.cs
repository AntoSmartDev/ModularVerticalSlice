using ModularVerticalSlice.SharedKernel;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;

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
        var entity = new Event { AvailableTickets = availableTickets };
        var result = entity.ReserveTickets(requestedQuantity);

        return result.IsFailure
            ? result
            : Result.Success();
    }
}
