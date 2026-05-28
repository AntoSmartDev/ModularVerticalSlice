using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Http;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.GetBookingDetails;

/// <summary>
/// Requests the details of a specific booking owned by the current user.
/// </summary>
/// <param name="BookingId">The booking identifier.</param>
public sealed record GetBookingDetailsQuery(Guid BookingId);

/// <summary>
/// Represents the details returned for a booking read request.
/// </summary>
public sealed record BookingDetailsReadModel(
    Guid Id,
    Guid EventId,
    int Quantity,
    BookingStatus Status,
    string UserId,
    Guid ClientRequestId,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handles the query that returns details for a specific booking.
/// </summary>
public sealed class GetBookingDetailsHandler(
    IBookingReadDbContext readDb,
    ICurrentUserContext currentUserContext)
{
    /// <summary>
    /// Returns details for a specific booking owned by the current authenticated user.
    /// </summary>
    public async Task<Result<BookingDetailsReadModel>> Handle(
        GetBookingDetailsQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserContext.UserId))
        {
            return Result.Failure<BookingDetailsReadModel>(
                Error.Unauthorized(
                    "Bookings.MissingCurrentUser",
                    "The current user is required to read booking details."));
        }

        var booking = await readDb.Bookings
            .Where(x => x.Id == query.BookingId && x.UserId == currentUserContext.UserId)
            .Select(x => new BookingDetailsReadModel(
                x.Id,
                x.EventId,
                x.Quantity,
                x.Status,
                x.UserId,
                x.ClientRequestId,
                x.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (booking is null)
        {
            return Result.Failure<BookingDetailsReadModel>(
                Error.NotFound(
                    "Bookings.BookingNotFound",
                    "The requested booking was not found."));
        }

        return booking;
    }
}

/// <summary>
/// Maps the endpoint that returns booking details for the current user.
/// </summary>
public static class GetBookingDetailsEndpoint
{
    /// <summary>
    /// Maps the HTTP endpoint for the booking-details slice.
    /// </summary>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/bookings/{id:guid}", GetBookingDetailsAsync)
            .WithTags("Bookings")
            .WithSummary("Returns booking details for the current user")
            .Produces<BookingDetailsReadModel>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetBookingDetailsAsync(
        Guid id,
        GetBookingDetailsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetBookingDetailsQuery(id), cancellationToken);
        return result.ToHttpResponse();
    }
}
