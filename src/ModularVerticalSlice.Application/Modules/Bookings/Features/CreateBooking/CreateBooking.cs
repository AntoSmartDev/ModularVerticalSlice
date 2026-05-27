using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Shared.Http;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.SharedKernel;
using Wolverine;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;

/// <summary>
/// Creates a new booking for a catalog event.
/// </summary>
/// <param name="EventId">The target event identifier.</param>
/// <param name="Quantity">The number of tickets requested for the booking.</param>
/// <param name="ClientRequestId">The client-side idempotency identifier.</param>
public record CreateBookingCommand(
    Guid EventId,
    int Quantity,
    Guid ClientRequestId);

/// <summary>
/// Transitional alias kept to avoid breaking the current baseline while the
/// Bookings flow is being realigned to the coordinated CreateBooking use case.
/// </summary>
/// <param name="EventId">The target event identifier.</param>
/// <param name="Quantity">The number of tickets requested for the booking.</param>
/// <param name="ClientRequestId">The client-side idempotency identifier.</param>
public sealed record RequestBookingCommand(
    Guid EventId,
    int Quantity,
    Guid ClientRequestId)
    : CreateBookingCommand(EventId, Quantity, ClientRequestId);

internal static class CreateBookingValidator
{
    public static Result Validate(CreateBookingCommand command)
    {
        if (command.EventId == Guid.Empty)
        {
            return Result.Failure(
                Error.Validation(
                    "Bookings.InvalidEventId",
                    "The event identifier is required."));
        }

        if (command.Quantity <= 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Bookings.InvalidQuantity",
                    "The booking quantity must be greater than zero."));
        }

        if (command.ClientRequestId == Guid.Empty)
        {
            return Result.Failure(
                Error.Validation(
                    "Bookings.InvalidClientRequestId",
                    "The client request identifier is required."));
        }

        return Result.Success();
    }

    public static Result Validate(RequestBookingCommand command) =>
        Validate((CreateBookingCommand)command);
}

/// <summary>
/// Handles the creation of a baseline pending booking.
/// </summary>
public sealed class CreateBookingHandler(
    IBookingWriteDbContext writeDb,
    IMessageBus bus,
    ICurrentUserContext currentUserContext,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Creates a new pending booking and coordinates the initial ticket reservation.
    /// </summary>
    public async Task<Result<Guid>> Handle(
        CreateBookingCommand command,
        CancellationToken cancellationToken)
    {
        var validation = CreateBookingValidator.Validate(command);
        if (validation.IsFailure)
        {
            return Result.Failure<Guid>(validation.Error);
        }

        if (string.IsNullOrWhiteSpace(currentUserContext.UserId))
        {
            return Error.Unauthorized(
                "Bookings.MissingCurrentUser",
                "The current user is required to create a booking.");
        }

        var existingBooking = await writeDb.Bookings
            .FirstOrDefaultAsync(
                x => x.UserId == currentUserContext.UserId &&
                     x.ClientRequestId == command.ClientRequestId,
                cancellationToken);

        if (existingBooking is not null)
        {
            return existingBooking.Id;
        }

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            EventId = command.EventId,
            Quantity = command.Quantity,
            Status = BookingStatus.Pending,
            UserId = currentUserContext.UserId,
            ClientRequestId = command.ClientRequestId,
            CreatedAt = timeProvider.GetUtcNow()
        };

        var reserveTicketsResult = await bus.InvokeAsync<Result>(
            new ReserveTicketsCommand(
                command.EventId,
                command.Quantity,
                booking.Id),
            cancellationToken);

        if (reserveTicketsResult.IsFailure)
        {
            return Result.Failure<Guid>(reserveTicketsResult.Error);
        }

        writeDb.Bookings.Add(booking);

        var bookingCreated = new BookingCreatedEvent(
            booking.Id,
            booking.EventId,
            booking.UserId,
            booking.Quantity,
            booking.CreatedAt);

        await bus.PublishAsync(bookingCreated);

        return booking.Id;
    }

    /// <summary>
    /// Handles the transitional request-booking alias.
    /// </summary>
    public Task<Result<Guid>> Handle(
        RequestBookingCommand command,
        CancellationToken cancellationToken) =>
        Handle((CreateBookingCommand)command, cancellationToken);
}

/// <summary>
/// Maps the baseline HTTP endpoint exposed by the create-booking slice.
/// </summary>
public static class CreateBookingEndpoint
{
    /// <summary>
    /// Maps the create-booking endpoint.
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
        CreateBookingHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(command, cancellationToken);
        return result.ToHttpResponse();
    }
}
