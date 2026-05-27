using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.ReleaseTickets;

/// <summary>
/// Handles ticket release for a specific catalog event.
/// </summary>
public sealed class ReleaseTicketsHandler(ICatalogWriteDbContext writeDb)
{
    /// <summary>
    /// Releases tickets back to an existing catalog event.
    /// </summary>
    public async Task<Result> Handle(
        ReleaseTicketsCommand command,
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

        return entity.ReleaseTickets(command.Quantity);
    }
}
