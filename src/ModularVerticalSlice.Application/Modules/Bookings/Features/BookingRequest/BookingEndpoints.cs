using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ModularVerticalSlice.Application.Shared.Http;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingRequest;

/// <summary>
/// Maps the baseline HTTP endpoints exposed by the Bookings request feature.
/// </summary>
public static class BookingEndpoints
{
    /// <summary>
    /// Maps the Bookings request endpoints.
    /// </summary>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/v1/bookings")
            .WithTags("Bookings");

        group.MapPost("/", CreateBookingAsync)
            .WithSummary("Creates a baseline booking request")
            .Produces<Guid>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return endpoints;
    }

    private static async Task<IResult> CreateBookingAsync(
        CreateBookingCommand command,
        BookingHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.ToHttpResponse();
    }
}
