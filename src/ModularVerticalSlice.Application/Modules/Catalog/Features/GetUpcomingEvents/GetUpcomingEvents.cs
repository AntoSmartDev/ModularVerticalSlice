using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Shared.Http;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.GetUpcomingEvents;

/// <summary>
/// Requests the list of upcoming catalog events.
/// </summary>
public sealed record GetUpcomingEventsQuery;

/// <summary>
/// Handles the query that returns the list of upcoming events.
/// </summary>
public sealed class GetUpcomingEventsHandler(
    ICatalogReadDbContext readDb,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Returns the list of upcoming catalog events.
    /// </summary>
    [WolverineHandler]
    public async Task<Result<IReadOnlyList<EventReadModel>>> HandleGetUpcomingEvents(
        GetUpcomingEventsQuery query,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var events = await readDb.Events
            .Where(x => x.Date >= now)
            .OrderBy(x => x.Date)
            .Select(x => new EventReadModel(
                x.Id,
                x.Title,
                x.Date,
                x.TicketPrice))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<EventReadModel>>(events);
    }
}

/// <summary>
/// Maps the endpoint that returns upcoming catalog events.
/// </summary>
public static class GetUpcomingEventsEndpoint
{
    /// <summary>
    /// Maps the HTTP endpoint for the upcoming-events slice.
    /// </summary>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/events/", GetUpcomingEventsAsync)
            .WithTags("Catalog")
            .WithSummary("Returns upcoming catalog events")
            .Produces<IReadOnlyList<EventReadModel>>(StatusCodes.Status200OK);

        return endpoints;
    }

    private static async Task<IResult> GetUpcomingEventsAsync(
        GetUpcomingEventsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleGetUpcomingEvents(new GetUpcomingEventsQuery(), cancellationToken);
        return result.ToHttpResponse();
    }
}
