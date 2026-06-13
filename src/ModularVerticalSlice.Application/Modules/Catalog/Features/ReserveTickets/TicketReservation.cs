using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Contracts;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.ReserveTickets;

/// <summary>
/// Applies a Catalog-owned reservation to the caller's shared scoped DbContext.
/// </summary>
public sealed class TicketReservation(ICatalogWriteDbContextSlice writeDb) : ITicketReservation
{
    /// <inheritdoc />
    public async Task<Result> ReserveAsync(
        Guid eventId,
        int quantity,
        Guid bookingId,
        CancellationToken cancellationToken)
    {
        var trackedEvent = await writeDb.Events
            .FirstOrDefaultAsync(x => x.Id == eventId, cancellationToken);

        if (trackedEvent is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "Catalog.EventNotFound",
                    "The requested event was not found."));
        }

        return trackedEvent.ReserveTickets(quantity);
    }
}
