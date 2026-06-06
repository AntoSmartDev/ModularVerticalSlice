using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.SharedKernel;
using Wolverine;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Starts the baseline booking lifecycle saga from a newly created booking event.
/// </summary>
/// <remarks>
/// This is the minimal local baseline before durable messaging, timeout
/// scheduling and payment processing are introduced. It marks the explicit saga
/// handoff point without yet implementing the full workflow.
/// </remarks>
public static class BookingLifecycleSagaHandler
{
    private static readonly TimeSpan PaymentWindowTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Transitional overload kept to preserve the pre-runtime baseline tests.
    /// </summary>
    [WolverineHandler]
    public static Task HandleBookingCreated(
        BookingCreatedEvent message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the initial booking-created event and starts the durable booking
    /// lifecycle runtime through Wolverine.
    /// </summary>
    [WolverineHandler]
    public static async Task HandleBookingCreated(
        BookingCreatedEvent message,
        IMessageBus bus,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(timeProvider);

        await StartPaymentWindowAsync(message, bus, timeProvider);
    }

    /// <summary>
    /// Handles the successful completion of payment for the current booking saga.
    /// </summary>
    [WolverineHandler]
    public static Task<Result> HandlePaymentSucceeded(
        PaymentSucceededEvent message,
        IBookingReadDbContext readDb,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        HandlePendingBookingStepAsync(
            message.BookingId,
            readDb,
            cancellationToken,
            () => bus.InvokeAsync<Result>(
                new ConfirmBookingCommand(message.BookingId),
                cancellationToken));

    /// <summary>
    /// Handles the business failure of payment for the current booking saga.
    /// </summary>
    [WolverineHandler]
    public static async Task<Result> HandlePaymentFailed(
        PaymentFailedEvent message,
        Guid eventId,
        int quantity,
        IBookingReadDbContext readDb,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(readDb);
        ArgumentNullException.ThrowIfNull(bus);

        var canProceed = await EnsureBookingIsPendingAsync(
            message.BookingId,
            readDb,
            cancellationToken);

        if (canProceed.IsFailure)
        {
            return canProceed;
        }

        if (!canProceed.Value)
        {
            return Result.Success();
        }

        return await HandleCompensationAsync(
            new CancelBookingCommand(message.BookingId),
            new ReleaseTicketsCommand(
                eventId,
                quantity,
                message.BookingId),
            bus,
            cancellationToken);
    }

    /// <summary>
    /// Handles booking payment timeout expiration for the current booking saga.
    /// </summary>
    [WolverineHandler]
    public static async Task<Result> HandlePaymentTimeoutExpired(
        BookingPaymentTimeoutExpiredEvent message,
        IBookingReadDbContext readDb,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(readDb);
        ArgumentNullException.ThrowIfNull(bus);

        var canProceed = await EnsureBookingIsPendingAsync(
            message.BookingId,
            readDb,
            cancellationToken);

        if (canProceed.IsFailure)
        {
            return canProceed;
        }

        if (!canProceed.Value)
        {
            return Result.Success();
        }

        return await HandleCompensationAsync(
            new ExpireBookingCommand(message.BookingId),
            new ReleaseTicketsCommand(
                message.EventId,
                message.Quantity,
                message.BookingId),
            bus,
            cancellationToken);
    }

    private static async Task StartPaymentWindowAsync(
        BookingCreatedEvent message,
        IMessageBus bus,
        TimeProvider timeProvider)
    {
        var expiresAt = timeProvider.GetUtcNow().Add(PaymentWindowTimeout);

        await bus.ScheduleAsync(
            new BookingPaymentTimeoutExpiredEvent(
                message.BookingId,
                expiresAt,
                message.EventId,
                message.Quantity),
            expiresAt,
            new DeliveryOptions());

        await bus.PublishAsync(
            new ProcessPaymentCommand(
                message.BookingId,
                message.EventId,
                message.UserId,
                message.Quantity));
    }

    private static async Task<Result> HandlePendingBookingStepAsync(
        Guid bookingId,
        IBookingReadDbContext readDb,
        CancellationToken cancellationToken,
        Func<Task<Result>> action)
    {
        ArgumentNullException.ThrowIfNull(readDb);
        ArgumentNullException.ThrowIfNull(action);

        var canProceed = await EnsureBookingIsPendingAsync(
            bookingId,
            readDb,
            cancellationToken);

        if (canProceed.IsFailure)
        {
            return Result.Failure(canProceed.Error);
        }

        if (!canProceed.Value)
        {
            return Result.Success();
        }

        return await action();
    }

    private static async Task<Result> HandleCompensationAsync(
        object transitionCommand,
        ReleaseTicketsCommand releaseTicketsCommand,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var transitionResult = await bus.InvokeAsync<Result>(
            transitionCommand,
            cancellationToken);

        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        await bus.PublishAsync(releaseTicketsCommand);

        return Result.Success();
    }

    private static async Task<Result<bool>> EnsureBookingIsPendingAsync(
        Guid bookingId,
        IBookingReadDbContext readDb,
        CancellationToken cancellationToken)
    {
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
