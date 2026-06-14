using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;
using Wolverine;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Confirms an existing pending booking after successful payment.
/// </summary>
/// <param name="BookingId">The target booking identifier.</param>
public sealed record ConfirmBookingCommand(Guid BookingId);

/// <summary>
/// Cancels an existing booking after a failure or business decision.
/// </summary>
/// <param name="BookingId">The target booking identifier.</param>
public sealed record CancelBookingCommand(Guid BookingId);

/// <summary>
/// Expires an existing pending booking after the payment window elapses.
/// </summary>
/// <param name="BookingId">The target booking identifier.</param>
public sealed record ExpireBookingCommand(Guid BookingId);

/// <summary>
/// Configures the BookingLifecycle orchestration baseline.
/// </summary>
public sealed class BookingLifecycleOptions
{
    /// <summary>
    /// Gets or sets how long a newly created booking may wait for payment.
    /// </summary>
    public TimeSpan PaymentWindow { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Handles the Bookings lifecycle state-transition commands.
/// </summary>
/// <remarks>
/// These handlers were evaluated for Wolverine EF Storage Operations (T002) and intentionally
/// left on the plain <see cref="Result"/> return shape. The <see cref="Booking"/> is already
/// loaded and tracked by the write DbContext, so the mutation is committed by
/// <c>AutoApplyTransactions()</c>; an explicit <c>Storage.Update</c> would be functionally
/// redundant and only add tuple noise across three handlers and the shared helper, without any
/// correctness or transactional gain. Storage Operations remain on the asynchronous Catalog
/// <c>ReleaseTickets</c> compensation, where the handler owns an independent transaction.
/// </remarks>
public sealed class BookingLifecycleHandler(
    IBookingWriteDbContextSlice writeDb,
    IMessageBus bus,
    TimeProvider timeProvider,
    ILogger<BookingLifecycleHandler> logger)
{
    private readonly IBookingWriteDbContextSlice _writeDb = writeDb ?? throw new ArgumentNullException(nameof(writeDb));
    private readonly IMessageBus _bus = bus ?? throw new ArgumentNullException(nameof(bus));
    private readonly TimeProvider _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    private readonly ILogger<BookingLifecycleHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <summary>
    /// Confirms an existing pending booking.
    /// </summary>
    [WolverineHandler]
    public async Task<Result> HandleConfirmBooking(
        ConfirmBookingCommand command,
        CancellationToken cancellationToken)
    {
        var booking = await LoadTrackedBookingAsync(command.BookingId, cancellationToken);
        if (booking is null)
        {
            return BookingNotFound();
        }

        var result = booking.Confirm();
        if (result.IsFailure)
        {
            return result;
        }

        _logger.LogInformation(
            "Booking {BookingId} transitioned to {BookingStatus}.",
            booking.Id,
            booking.Status);

        await _bus.PublishAsync(
            new BookingConfirmedEvent(
                booking.Id,
                booking.EventId,
                booking.UserId,
                _timeProvider.GetUtcNow()));

        return Result.Success();
    }

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
        var booking = await LoadTrackedBookingAsync(bookingId, cancellationToken);

        if (booking is null)
        {
            return BookingNotFound();
        }

        var result = transition(booking);
        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "Booking {BookingId} transitioned to {BookingStatus}.",
                booking.Id,
                booking.Status);
        }

        return result;
    }

    private Task<Booking?> LoadTrackedBookingAsync(Guid bookingId, CancellationToken cancellationToken) =>
        _writeDb.Bookings.FirstOrDefaultAsync(x => x.Id == bookingId, cancellationToken);

    private static Result BookingNotFound() =>
        Result.Failure(
            Error.NotFound(
                "Bookings.BookingNotFound",
                "The target booking was not found."));
}
