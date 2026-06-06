using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Handles the Bookings lifecycle state-transition commands.
/// </summary>
public sealed class BookingLifecycleHandler(IBookingWriteDbContext writeDb)
{
    /// <summary>
    /// Confirms an existing pending booking.
    /// </summary>
    [WolverineHandler]
    public Task<Result> HandleConfirmBooking(
        ConfirmBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyTransitionAsync(command.BookingId, cancellationToken, booking => booking.Confirm());

    /// <summary>
    /// Cancels an existing pending booking.
    /// </summary>
    [WolverineHandler]
    public Task<Result> HandleCancelBooking(
        CancelBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyTransitionAsync(command.BookingId, cancellationToken, booking => booking.Cancel());

    /// <summary>
    /// Expires an existing pending booking.
    /// </summary>
    [WolverineHandler]
    public Task<Result> HandleExpireBooking(
        ExpireBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyTransitionAsync(command.BookingId, cancellationToken, booking => booking.Expire());

    private async Task<Result> ApplyTransitionAsync(
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
