using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Modules.Catalog.Messages;
using ModularVerticalSlice.Modules.Catalog.Persistence;
using ModularVerticalSlice.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Modules.Catalog.Features.Events;

/// <summary>
/// Handles Catalog event commands and queries.
/// </summary>
/// <remarks>
/// This handler uses only Catalog mini DbContext contracts. It must never depend
/// on the concrete AppDbContext and must never call SaveChangesAsync directly.
/// </remarks>
public sealed class EventHandler(
    ICatalogWriteDbContext writeDb,
    ICatalogReadDbContext readDb,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Handles event creation in the local Catalog module flow.
    /// </summary>
    public Task<Result<EventReadModel>> Handle(
        CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        var validation = EventValidators.Validate(command);
        if (validation.IsFailure)
        {
            return Task.FromResult(Result.Failure<EventReadModel>(validation.Error));
        }

        var entity = new Event
        {
            Id = Guid.NewGuid(),
            Title = command.Title.Trim(),
            Date = command.Date,
            TicketPrice = command.TicketPrice,
            AvailableTickets = command.AvailableTickets
        };

        writeDb.Events.Add(entity);

        return Task.FromResult<Result<EventReadModel>>(
            new EventReadModel(
                entity.Id,
                entity.Title,
                entity.Date,
                entity.TicketPrice));
    }

    /// <summary>
    /// Handles the query that returns the list of upcoming events.
    /// </summary>
    public async Task<Result<IReadOnlyList<EventReadModel>>> Handle(
        GetUpcomingEventsQuery query,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var events = await readDb.Events
            .UpcomingOnly(now)
            .OrderBy(x => x.Date)
            .ToReadModels()
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<EventReadModel>>(events);
    }

    /// <summary>
    /// Handles the query that returns the details of a single event.
    /// </summary>
    public async Task<Result<EventReadModel>> Handle(
        GetEventDetailsQuery query,
        CancellationToken cancellationToken)
    {
        var eventDetails = await readDb.Events
            .Where(x => x.Id == query.EventId)
            .ToReadModels()
            .FirstOrDefaultAsync(cancellationToken);

        if (eventDetails is null)
        {
            return Error.NotFound(
                "Catalog.EventNotFound",
                "The requested event was not found.");
        }

        return eventDetails;
    }
}
