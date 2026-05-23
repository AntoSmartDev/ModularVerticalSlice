using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingRequest;

/// <summary>
/// Handles Bookings request commands for the baseline booking flow.
/// </summary>
/// <remarks>
/// This handler uses only Bookings mini DbContext contracts and shared module
/// abstractions. It must not depend on the concrete AppDbContext and must not
/// call SaveChangesAsync directly.
/// </remarks>
public sealed class BookingHandler(
    IBookingWriteDbContext writeDb,
    ICurrentUserContext currentUserContext,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Handles the creation of a baseline pending booking.
    /// </summary>
    public async Task<Result<Guid>> Handle(
        RequestBookingCommand command,
        CancellationToken cancellationToken)
    {
        var validation = BookingValidators.Validate(command);
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

        writeDb.Bookings.Add(booking);

        return booking.Id;
    }
}
