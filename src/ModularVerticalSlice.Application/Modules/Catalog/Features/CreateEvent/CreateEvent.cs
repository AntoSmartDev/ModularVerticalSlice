using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Http;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Catalog.Features.CreateEvent;

/// <summary>
/// Requests the creation of a new catalog event.
/// </summary>
/// <param name="Title">The display title of the event.</param>
/// <param name="Date">The scheduled date and time of the event.</param>
/// <param name="TicketPrice">The ticket price configured for the event.</param>
/// <param name="AvailableTickets">The initial number of tickets available for reservation.</param>
public sealed record CreateEventCommand(
    string Title,
    DateTimeOffset Date,
    decimal TicketPrice,
    int AvailableTickets);

internal static class CreateEventValidator
{
    public static Result Validate(CreateEventCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.Title))
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidEventTitle",
                    "The event title is required."));
        }

        if (command.TicketPrice <= 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidTicketPrice",
                    "The ticket price must be greater than zero."));
        }

        if (command.AvailableTickets < 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidAvailableTickets",
                    "The available ticket count cannot be negative."));
        }

        return Result.Success();
    }
}

/// <summary>
/// Handles event creation in the local Catalog module flow.
/// </summary>
public sealed class CreateEventHandler(ICatalogWriteDbContext writeDb)
{
    /// <summary>
    /// Creates a new catalog event in the write-side persistence boundary.
    /// </summary>
    [WolverineHandler]
    public Task<Result<EventReadModel>> HandleCreateEvent(
        CreateEventCommand command,
        CancellationToken cancellationToken)
    {
        var validation = CreateEventValidator.Validate(command);
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
}

/// <summary>
/// Maps the endpoint that creates catalog events.
/// </summary>
public static class CreateEventEndpoint
{
    /// <summary>
    /// Maps the HTTP endpoint for the create-event slice.
    /// </summary>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/v1/events/", CreateEventAsync)
            .WithTags("Catalog")
            .WithSummary("Creates a catalog event")
            .Produces<EventReadModel>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }

    private static async Task<IResult> CreateEventAsync(
        CreateEventCommand command,
        CreateEventHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleCreateEvent(command, cancellationToken);
        return result.ToHttpResponse();
    }
}
