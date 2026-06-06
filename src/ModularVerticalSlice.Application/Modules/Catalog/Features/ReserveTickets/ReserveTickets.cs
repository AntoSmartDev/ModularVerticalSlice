using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.ReserveTickets;

/// <summary>
/// Handles ticket reservation for a specific catalog event.
/// </summary>
public sealed class ReserveTicketsHandler(ICatalogWriteDbContext writeDb)
{
    /// <summary>
    /// Reserves tickets for an existing catalog event.
    /// </summary>
    [WolverineHandler]
    public async Task<Result> HandleReserveTickets(
        ReserveTicketsCommand command,
        CancellationToken cancellationToken)
    {
        var entity = await writeDb.Events
            .FirstOrDefaultAsync(x => x.Id == command.EventId, cancellationToken);

        if (entity is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "Catalog.EventNotFound",
                    "The requested event was not found."));
        }

        var reservationResult = entity.ReserveTickets(command.Quantity);

        if (reservationResult.IsFailure)
        {
            return reservationResult;
        }

        return Result.Success();
    }
}
