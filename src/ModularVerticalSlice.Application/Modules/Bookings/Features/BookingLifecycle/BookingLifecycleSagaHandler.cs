using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using Wolverine;

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
    private static readonly TimeSpan PaymentTimeout = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Transitional overload kept to preserve the pre-runtime baseline tests.
    /// </summary>
    public static Task Handle(
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
    public static async Task Handle(
        BookingCreatedEvent message,
        IMessageBus bus,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var expiresAt = timeProvider.GetUtcNow().Add(PaymentTimeout);

        await bus.ScheduleAsync(
            new BookingPaymentTimeoutExpiredEvent(
                message.BookingId,
                expiresAt),
            expiresAt,
            new DeliveryOptions());

        await bus.PublishAsync(
            new ProcessPaymentCommand(
                message.BookingId,
                message.EventId,
                message.UserId,
                message.Quantity));
    }
}
