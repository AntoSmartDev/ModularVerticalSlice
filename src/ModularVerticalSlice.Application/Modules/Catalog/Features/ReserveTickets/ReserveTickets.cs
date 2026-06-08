using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.ReserveTickets;

/// <summary>
/// Handles ticket reservation for a specific catalog event.
/// </summary>
[Transactional]
public sealed class ReserveTicketsHandler(ICatalogWriteDbContext writeDb)
{
    private readonly ICatalogWriteDbContext _writeDb = writeDb ?? throw new ArgumentNullException(nameof(writeDb));

    /// <summary>
    /// Reserves tickets for an existing catalog event.
    /// </summary>
    [WolverineHandler]
    public async Task<Result> HandleReserveTickets(
        ReserveTicketsCommand command,
        CancellationToken cancellationToken)
    {
        var entity = await _writeDb.Events
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
