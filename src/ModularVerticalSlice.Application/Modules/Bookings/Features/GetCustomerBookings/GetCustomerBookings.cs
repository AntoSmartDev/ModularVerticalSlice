using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Http;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.GetCustomerBookings;

/// <summary>
/// Requests the bookings that belong to the current user.
/// </summary>
public sealed record GetCustomerBookingsQuery;

/// <summary>
/// Represents a booking row returned by the current-user bookings query.
/// </summary>
public sealed record CustomerBookingReadModel(
    Guid Id,
    Guid EventId,
    int Quantity,
    BookingStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handles the query that returns bookings owned by the current user.
/// </summary>
public sealed class GetCustomerBookingsHandler(
    IBookingReadDbContext readDb,
    ICurrentUserContext currentUserContext)
{
    /// <summary>
    /// Returns the bookings owned by the current authenticated user.
    /// </summary>
    public async Task<Result<IReadOnlyList<CustomerBookingReadModel>>> Handle(
        GetCustomerBookingsQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(currentUserContext.UserId))
        {
            return Result.Failure<IReadOnlyList<CustomerBookingReadModel>>(
                Error.Unauthorized(
                    "Bookings.MissingCurrentUser",
                    "The current user is required to read bookings."));
        }

        var bookings = await readDb.Bookings
            .Where(x => x.UserId == currentUserContext.UserId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new CustomerBookingReadModel(
                x.Id,
                x.EventId,
                x.Quantity,
                x.Status,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<CustomerBookingReadModel>>(bookings);
    }
}

/// <summary>
/// Maps the endpoint that returns bookings for the current user.
/// </summary>
public static class GetCustomerBookingsEndpoint
{
    /// <summary>
    /// Maps the HTTP endpoint for the customer-bookings slice.
    /// </summary>
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/bookings/", GetCustomerBookingsAsync)
            .WithTags("Bookings")
            .WithSummary("Returns the current user's bookings")
            .Produces<IReadOnlyList<CustomerBookingReadModel>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static async Task<IResult> GetCustomerBookingsAsync(
        GetCustomerBookingsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetCustomerBookingsQuery(), cancellationToken);
        return result.ToHttpResponse();
    }
}
