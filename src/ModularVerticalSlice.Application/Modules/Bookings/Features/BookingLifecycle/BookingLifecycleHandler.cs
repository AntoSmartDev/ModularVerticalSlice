using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.SharedKernel;
using CatalogEventHandler = ModularVerticalSlice.Application.Modules.Catalog.Features.Events.EventHandler;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Handles Bookings lifecycle commands for the baseline booking flow.
/// </summary>
/// <remarks>
/// This handler uses only Bookings mini DbContext contracts and shared module
/// abstractions. It must not depend on the concrete AppDbContext and must not
/// call SaveChangesAsync directly.
/// </remarks>
public sealed class BookingLifecycleHandler(
    IBookingWriteDbContext writeDb,
    CatalogEventHandler catalogHandler,
    ICurrentUserContext currentUserContext,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Handles the creation of a baseline pending booking.
    /// </summary>
    public async Task<Result<Guid>> Handle(
        CreateBookingCommand command,
        CancellationToken cancellationToken)
    {
        var validation = BookingLifecycleValidators.Validate(command);
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

        var reserveTicketsResult = await catalogHandler.Handle(
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

        await BookingLifecycleSagaHandler.Handle(
            bookingCreated,
            cancellationToken);

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
