using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.SharedKernel;
using Wolverine;
using Wolverine.Persistence.Sagas;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Holds the minimum durable state required to correlate and compensate a booking lifecycle.
/// </summary>
/// <remarks>
/// The saga identity is the booking identifier. Booking status and other domain state remain
/// owned by the Booking entity instead of being duplicated in this orchestration state.
/// Wolverine saga classes require the runtime method names <c>Start</c> and <c>Handle</c>;
/// explicit <see cref="SagaIdentityFromAttribute"/> annotations keep their correlation intent clear.
/// </remarks>
public sealed class BookingLifecycleSaga : Saga
{
    /// <summary>
    /// Gets or sets the booking identifier used as the saga correlation identity.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the event identifier required when ticket release compensation is needed.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Gets or sets the ticket quantity required when ticket release compensation is needed.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets when the booking lifecycle started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>
    /// Starts and persists the saga, then opens the configured payment window.
    /// </summary>
    public static async Task<BookingLifecycleSaga> Start(
        [SagaIdentityFrom(nameof(BookingCreatedEvent.BookingId))]
        BookingCreatedEvent message,
        IMessageBus bus,
        TimeProvider timeProvider,
        IOptions<BookingLifecycleOptions> options)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);

        var paymentWindow = options.Value.PaymentWindow;
        if (paymentWindow <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("The booking lifecycle payment window must be greater than zero.");
        }

        var saga = new BookingLifecycleSaga
        {
            Id = message.BookingId,
            EventId = message.EventId,
            Quantity = message.Quantity,
            StartedAt = message.CreatedAt
        };

        var expiresAt = timeProvider.GetUtcNow().Add(paymentWindow);

        await bus.ScheduleAsync(
            new BookingPaymentTimeoutExpiredEvent(message.BookingId, expiresAt),
            expiresAt,
            new DeliveryOptions());

        await bus.PublishAsync(
            new ProcessPaymentCommand(
                message.BookingId,
                message.EventId,
                message.UserId,
                message.Quantity,
                expiresAt));

        return saga;
    }

    /// <summary>
    /// Handles successful payment and completes the correlated saga after confirmation.
    /// </summary>
    public async Task<Result> Handle(
        [SagaIdentityFrom(nameof(PaymentSucceededEvent.BookingId))]
        PaymentSucceededEvent message,
        IBookingReadDbContextSlice readDb,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var canProceed = await EnsureBookingIsPendingAsync(message.BookingId, readDb, cancellationToken);
        if (canProceed.IsFailure)
        {
            return Result.Failure(canProceed.Error);
        }

        if (!canProceed.Value)
        {
            MarkCompleted();
            return Result.Success();
        }

        var result = await bus.InvokeAsync<Result>(
            new ConfirmBookingCommand(message.BookingId),
            cancellationToken);

        CompleteWhenSuccessful(result);
        return result;
    }

    /// <summary>
    /// Handles business payment failure and compensates the correlated saga.
    /// </summary>
    public async Task<Result> Handle(
        [SagaIdentityFrom(nameof(PaymentFailedEvent.BookingId))]
        PaymentFailedEvent message,
        IBookingReadDbContextSlice readDb,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        await HandleCompensationAsync(
            message.BookingId,
            new CancelBookingCommand(message.BookingId),
            readDb,
            bus,
            cancellationToken);

    /// <summary>
    /// Handles payment timeout and compensates the correlated saga.
    /// </summary>
    public async Task<Result> Handle(
        [SagaIdentityFrom(nameof(BookingPaymentTimeoutExpiredEvent.BookingId))]
        BookingPaymentTimeoutExpiredEvent message,
        IBookingReadDbContextSlice readDb,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        await HandleCompensationAsync(
            message.BookingId,
            new ExpireBookingCommand(message.BookingId),
            readDb,
            bus,
            cancellationToken);

    /// <summary>
    /// Ignores a successful-payment message after the saga has already completed.
    /// </summary>
    public static void NotFound(PaymentSucceededEvent message) =>
        ArgumentNullException.ThrowIfNull(message);

    /// <summary>
    /// Ignores a failed-payment message after the saga has already completed.
    /// </summary>
    public static void NotFound(PaymentFailedEvent message) =>
        ArgumentNullException.ThrowIfNull(message);

    /// <summary>
    /// Ignores a timeout message after the saga has already completed.
    /// </summary>
    public static void NotFound(BookingPaymentTimeoutExpiredEvent message) =>
        ArgumentNullException.ThrowIfNull(message);

    private async Task<Result> HandleCompensationAsync(
        Guid bookingId,
        object transitionCommand,
        IBookingReadDbContextSlice readDb,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var canProceed = await EnsureBookingIsPendingAsync(bookingId, readDb, cancellationToken);
        if (canProceed.IsFailure)
        {
            return Result.Failure(canProceed.Error);
        }

        if (!canProceed.Value)
        {
            MarkCompleted();
            return Result.Success();
        }

        var transitionResult = await bus.InvokeAsync<Result>(
            transitionCommand,
            cancellationToken);

        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        await bus.PublishAsync(new ReleaseTicketsCommand(EventId, Quantity, bookingId));
        MarkCompleted();

        return Result.Success();
    }

    private void CompleteWhenSuccessful(Result result)
    {
        if (result.IsSuccess)
        {
            MarkCompleted();
        }
    }

    private static async Task<Result<bool>> EnsureBookingIsPendingAsync(
        Guid bookingId,
        IBookingReadDbContextSlice readDb,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(readDb);

        var bookingStatus = await readDb.Bookings
            .Where(x => x.Id == bookingId)
            .Select(x => (BookingStatus?)x.Status)
            .SingleOrDefaultAsync(cancellationToken);

        if (bookingStatus is null)
        {
            return Error.NotFound(
                "Bookings.BookingNotFound",
                "The target booking was not found.");
        }

        return bookingStatus == BookingStatus.Pending;
    }
}
