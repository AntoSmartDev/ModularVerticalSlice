using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Http;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.SharedKernel;
using Wolverine;
using Wolverine.Attributes;

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
    string EventTitle,
    DateTimeOffset EventDate,
    Guid EventId,
    int Quantity,
    BookingStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// Handles the query that returns bookings owned by the current user.
/// </summary>
public sealed class GetCustomerBookingsHandler(
    IBookingCatalogReadDbContext readDb,
    ICurrentUserContext currentUserContext)
{
    /// <summary>
    /// Returns the bookings owned by the current authenticated user.
    /// </summary>
    [WolverineHandler]
    public async Task<Result<IReadOnlyList<CustomerBookingReadModel>>> HandleGetCustomerBookings(
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

        // Pragmatic same-store read-side composition:
        // one query on the shared store, at the cost of weaker read isolation.
        // This is a local query compromise, not the default pattern for
        // cross-module collaboration or for write flows.
        var bookings = await readDb.Bookings
            .Where(x => x.UserId == currentUserContext.UserId)
            .Join(
                readDb.Events,
                booking => booking.EventId,
                @event => @event.Id,
                (booking, @event) => new
                {
                    Booking = booking,
                    Event = @event
                })
            .OrderByDescending(x => x.Booking.CreatedAt)
            .Select(x => new CustomerBookingReadModel(
                x.Booking.Id,
                x.Event.Title,
                x.Event.Date,
                x.Booking.EventId,
                x.Booking.Quantity,
                x.Booking.Status,
                x.Booking.CreatedAt))
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
        [FromServices] IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var result = await bus.InvokeAsync<Result<IReadOnlyList<CustomerBookingReadModel>>>(
            new GetCustomerBookingsQuery(),
            cancellationToken);
        return result.ToHttpResponse();
    }
}
