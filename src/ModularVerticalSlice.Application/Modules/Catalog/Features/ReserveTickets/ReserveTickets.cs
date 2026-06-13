using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;
using Wolverine.Persistence;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.ReserveTickets;

/// <summary>
/// Handles ticket reservation for a specific catalog event.
/// </summary>
public sealed class ReserveTicketsHandler(ICatalogWriteDbContextSlice writeDb)
{
    private readonly ICatalogWriteDbContextSlice _writeDb = writeDb ?? throw new ArgumentNullException(nameof(writeDb));

    /// <summary>
    /// Reserves tickets for an existing catalog event.
    /// </summary>
    /// <remarks>
    /// The handler returns a Wolverine EF storage side effect together with the business
    /// <see cref="Result"/>:
    /// <list type="bullet">
    /// <item><see cref="Storage.Update{T}(T)"/> on success makes the persistence intent explicit
    /// and lets the Wolverine runtime own the EF transaction around the commit.</item>
    /// <item><see cref="Storage.Nothing{T}"/> on a not-found or business-failure path leaves
    /// persistence untouched.</item>
    /// </list>
    /// The reservation always runs through the bus (<c>InvokeAsync</c>), so the side effect is
    /// processed by the Wolverine runtime; the <see cref="Result"/> member is returned to the caller.
    /// </remarks>
    [WolverineHandler]
    public async Task<(Result, IStorageAction<Event>)> HandleReserveTickets(
        ReserveTicketsCommand command,
        CancellationToken cancellationToken)
    {
        var trackedEvent = await _writeDb.Events
            .FirstOrDefaultAsync(x => x.Id == command.EventId, cancellationToken);

        if (trackedEvent is null)
        {
            return (
                Result.Failure(
                    Error.NotFound(
                        "Catalog.EventNotFound",
                        "The requested event was not found.")),
                Storage.Nothing<Event>());
        }

        var reservationResult = trackedEvent.ReserveTickets(command.Quantity);

        if (reservationResult.IsFailure)
        {
            return (reservationResult, Storage.Nothing<Event>());
        }

        return (Result.Success(), Storage.Update(trackedEvent));
    }
}
