using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;
using Wolverine.Persistence;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Handles the Bookings lifecycle state-transition commands.
/// </summary>
public sealed class BookingLifecycleHandler(IBookingWriteDbContext writeDb)
{
    private readonly IBookingWriteDbContext _writeDb = writeDb ?? throw new ArgumentNullException(nameof(writeDb));

    /// <summary>
    /// Confirms an existing pending booking.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="Storage.Update{T}(T)"/> when the transition succeeds so that the
    /// Wolverine runtime owns the EF transaction, and <see cref="Storage.Nothing{T}"/> when
    /// the booking is not found or the transition is invalid (no persistence side effect needed).
    /// The saga always invokes this via <c>bus.InvokeAsync&lt;Result&gt;</c>, so the
    /// <see cref="Result"/> member is returned to the caller and the storage action is processed
    /// by the runtime.
    /// </remarks>
    [WolverineHandler]
    public Task<(Result, IStorageAction<Booking>)> HandleConfirmBooking(
        ConfirmBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyTransitionAsync(command.BookingId, cancellationToken, booking => booking.Confirm());

    /// <summary>
    /// Cancels an existing pending booking.
    /// </summary>
    /// <remarks>
    /// See <see cref="HandleConfirmBooking"/> for the storage-action semantics.
    /// </remarks>
    [WolverineHandler]
    public Task<(Result, IStorageAction<Booking>)> HandleCancelBooking(
        CancelBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyTransitionAsync(command.BookingId, cancellationToken, booking => booking.Cancel());

    /// <summary>
    /// Expires an existing pending booking.
    /// </summary>
    /// <remarks>
    /// See <see cref="HandleConfirmBooking"/> for the storage-action semantics.
    /// </remarks>
    [WolverineHandler]
    public Task<(Result, IStorageAction<Booking>)> HandleExpireBooking(
        ExpireBookingCommand command,
        CancellationToken cancellationToken) =>
        ApplyTransitionAsync(command.BookingId, cancellationToken, booking => booking.Expire());

    private async Task<(Result, IStorageAction<Booking>)> ApplyTransitionAsync(
        Guid bookingId,
        CancellationToken cancellationToken,
        Func<Booking, Result> transition)
    {
        var booking = await _writeDb.Bookings
            .FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);

        if (booking is null)
        {
            return (
                Result.Failure(
                    Error.NotFound(
                        "Bookings.BookingNotFound",
                        "The target booking was not found.")),
                Storage.Nothing<Booking>());
        }

        var result = transition(booking);

        return result.IsSuccess
            ? (result, Storage.Update(booking))
            : (result, Storage.Nothing<Booking>());
    }
}
