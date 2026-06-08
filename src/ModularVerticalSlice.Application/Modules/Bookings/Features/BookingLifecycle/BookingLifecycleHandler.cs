using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Handles the Bookings lifecycle state-transition commands.
/// </summary>
/// <remarks>
/// These handlers were evaluated for Wolverine EF Storage Operations (T002) and intentionally
/// left on the plain <see cref="Result"/> return shape. The <see cref="Booking"/> is already
/// loaded and tracked by the write DbContext, so the mutation is committed by
/// <c>AutoApplyTransactions()</c>; an explicit <c>Storage.Update</c> would be functionally
/// redundant and only add tuple noise across three handlers and the shared helper, without any
/// correctness or transactional gain. Storage Operations are kept where they read naturally
/// (the Catalog <c>ReserveTickets</c>/<c>ReleaseTickets</c> pair) instead of applied uniformly.
/// </remarks>
public sealed class BookingLifecycleHandler(IBookingWriteDbContext writeDb)
{
    private readonly IBookingWriteDbContext _writeDb = writeDb ?? throw new ArgumentNullException(nameof(writeDb));

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
        Func<Booking, Result> transition)
    {
        var booking = await _writeDb.Bookings
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
