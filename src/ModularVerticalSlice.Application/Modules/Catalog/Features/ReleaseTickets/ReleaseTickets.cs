using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;
using Wolverine.Persistence;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.ReleaseTickets;

/// <summary>
/// Handles ticket release for a specific catalog event.
/// </summary>
public sealed class ReleaseTicketsHandler(ICatalogWriteDbContext writeDb)
{
    private readonly ICatalogWriteDbContext _writeDb = writeDb ?? throw new ArgumentNullException(nameof(writeDb));

    /// <summary>
    /// Releases tickets back to an existing catalog event.
    /// </summary>
    /// <remarks>
    /// The handler returns a Wolverine EF storage side effect together with the business
    /// <see cref="Result"/>:
    /// <list type="bullet">
    /// <item><see cref="Storage.Update{T}(T)"/> on success makes the persistence intent explicit
    /// and lets the Wolverine runtime own the EF transaction around the commit.</item>
    /// <item><see cref="Storage.Nothing{T}"/> on a not-found path leaves persistence untouched.</item>
    /// </list>
    /// The release always runs through the bus (<c>PublishAsync</c>) from the saga compensation
    /// path, so the side effect is processed by the Wolverine runtime.
    /// </remarks>
    [WolverineHandler]
    public async Task<(Result, IStorageAction<Event>)> HandleReleaseTickets(
        ReleaseTicketsCommand command,
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

        var releaseResult = trackedEvent.ReleaseTickets(command.Quantity);

        if (releaseResult.IsFailure)
        {
            return (releaseResult, Storage.Nothing<Event>());
        }

        return (Result.Success(), Storage.Update(trackedEvent));
    }
}
