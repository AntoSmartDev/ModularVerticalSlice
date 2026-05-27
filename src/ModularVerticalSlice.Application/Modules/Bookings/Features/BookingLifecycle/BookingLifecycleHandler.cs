using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Handles the Bookings lifecycle state-transition commands.
/// </summary>
public sealed class BookingLifecycleHandler(IBookingWriteDbContext writeDb)
{
    /// <summary>
    /// Confirms an existing pending booking.
    /// </summary>
    public Task<Result> Handle(
        ConfirmBookingCommand command,
        CancellationToken cancellationToken) =>
        Handle(command.BookingId, cancellationToken, booking => booking.Confirm());

    /// <summary>
    /// Cancels an existing pending booking.
    /// </summary>
    public Task<Result> Handle(
        CancelBookingCommand command,
        CancellationToken cancellationToken) =>
        Handle(command.BookingId, cancellationToken, booking => booking.Cancel());

    /// <summary>
    /// Expires an existing pending booking.
    /// </summary>
    public Task<Result> Handle(
        ExpireBookingCommand command,
        CancellationToken cancellationToken) =>
        Handle(command.BookingId, cancellationToken, booking => booking.Expire());

    private async Task<Result> Handle(
        Guid bookingId,
        CancellationToken cancellationToken,
        Func<Persistence.Entities.Booking, Result> transition)
    {
        var booking = await writeDb.Bookings
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "Bookings.BookingNotFound",
                    "The target booking was not found."));
        }

        return transition(booking);
    }
}
