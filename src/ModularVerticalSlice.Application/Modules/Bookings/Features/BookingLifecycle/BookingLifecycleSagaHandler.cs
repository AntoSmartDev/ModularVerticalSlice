using ModularVerticalSlice.Application.Modules.Bookings.Messages;

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
    /// <summary>
    /// Handles the initial booking-created event that will later start the
    /// durable booking lifecycle saga.
    /// </summary>
    public static Task Handle(
        BookingCreatedEvent message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.CompletedTask;
    }
}
