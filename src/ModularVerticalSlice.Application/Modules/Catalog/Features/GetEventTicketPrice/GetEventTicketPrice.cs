using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.GetEventTicketPrice;

/// <summary>
/// Handles the public query for the current ticket price of a catalog event.
/// </summary>
public sealed class GetEventTicketPriceHandler(ICatalogReadDbContextSlice readDb)
{
    /// <summary>
    /// Returns the current ticket price of a single catalog event.
    /// </summary>
    [WolverineHandler]
    public async Task<Result<decimal>> HandleGetEventTicketPrice(
        GetEventTicketPriceQuery query,
        CancellationToken cancellationToken)
    {
        var ticketPrice = await readDb.Events
            .Where(x => x.Id == query.EventId)
            .Select(x => (decimal?)x.TicketPrice)
            .SingleOrDefaultAsync(cancellationToken);

        if (ticketPrice is null)
        {
            return Error.NotFound(
                "Catalog.EventNotFound",
                "The requested event was not found.");
        }

        return ticketPrice.Value;
    }
}
