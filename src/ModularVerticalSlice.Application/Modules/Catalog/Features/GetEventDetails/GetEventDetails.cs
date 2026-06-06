using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Shared.Http;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.GetEventDetails;

/// <summary>
/// Requests the details of a specific catalog event.
/// </summary>
/// <param name="EventId">The event identifier.</param>
public sealed record GetEventDetailsQuery(Guid EventId);

/// <summary>
/// Handles the query that returns the details of a single event.
/// </summary>
public sealed class GetEventDetailsHandler(ICatalogReadDbContext readDb)
{
    /// <summary>
    /// Returns the details of a single catalog event.
    /// </summary>
    [WolverineHandler]
    public async Task<Result<EventReadModel>> HandleGetEventDetails(
        GetEventDetailsQuery query,
        CancellationToken cancellationToken)
    {
        var eventDetails = await readDb.Events
            .Where(x => x.Id == query.EventId)
            .Select(x => new EventReadModel(
                x.Id,
                x.Title,
                x.Date,
                x.TicketPrice))
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

/// <summary>
/// Maps the endpoint that returns catalog event details.
/// </summary>
public static class GetEventDetailsEndpoint
{
    /// <summary>
    /// Maps the HTTP endpoint for the event-details slice.
    /// </summary>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/events/{id:guid}", GetEventDetailsAsync)
            .WithTags("Catalog")
            .WithSummary("Returns catalog event details")
            .Produces<EventReadModel>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetEventDetailsAsync(
        Guid id,
        GetEventDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleGetEventDetails(new GetEventDetailsQuery(id), cancellationToken);
        return result.ToHttpResponse();
    }
}
