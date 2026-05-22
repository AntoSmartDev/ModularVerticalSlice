using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularVerticalSlice.Modules.Catalog.Messages;
using ModularVerticalSlice.Modules.Shared.Http;

namespace ModularVerticalSlice.Modules.Catalog.Features.Events;

/// <summary>
/// Maps the baseline HTTP endpoints exposed by the Catalog events feature.
/// </summary>
public static class EventEndpoints
{
    /// <summary>
    /// Maps the Catalog events endpoints.
    /// </summary>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/events")
            .WithTags("Catalog");

        group.MapPost("/", CreateEventAsync)
            .WithSummary("Creates a catalog event")
            .Produces<EventReadModel>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/", GetUpcomingEventsAsync)
            .WithSummary("Returns upcoming catalog events")
            .Produces<IReadOnlyList<EventReadModel>>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetEventDetailsAsync)
            .WithSummary("Returns catalog event details")
            .Produces<EventReadModel>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> CreateEventAsync(
        CreateEventCommand command,
        EventHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> GetUpcomingEventsAsync(
        EventHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetUpcomingEventsQuery(), cancellationToken);
        return result.ToHttpResponse();
    }

    private static async Task<IResult> GetEventDetailsAsync(
        Guid id,
        EventHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetEventDetailsQuery(id), cancellationToken);
        return result.ToHttpResponse();
    }
}
