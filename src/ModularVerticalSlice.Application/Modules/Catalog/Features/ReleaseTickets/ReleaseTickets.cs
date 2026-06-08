using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.ReleaseTickets;

/// <summary>
/// Handles ticket release for a specific catalog event.
/// </summary>
[Transactional]
public sealed class ReleaseTicketsHandler(ICatalogWriteDbContext writeDb)
{
    private readonly ICatalogWriteDbContext _writeDb = writeDb ?? throw new ArgumentNullException(nameof(writeDb));

    /// <summary>
    /// Releases tickets back to an existing catalog event.
    /// </summary>
    [WolverineHandler]
    public async Task<Result> HandleReleaseTickets(
        ReleaseTicketsCommand command,
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

        return entity.ReleaseTickets(command.Quantity);
    }
}
