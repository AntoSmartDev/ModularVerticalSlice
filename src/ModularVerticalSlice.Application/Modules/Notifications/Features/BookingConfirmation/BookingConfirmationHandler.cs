using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Notifications.Features.BookingConfirmation;

/// <summary>
/// Handles booking confirmation notifications.
/// </summary>
public sealed class BookingConfirmationHandler(IBookingConfirmationEmailSender emailSender)
{
    /// <summary>
    /// Sends the confirmation email represented by a confirmed booking event.
    /// </summary>
    [WolverineHandler]
    public Task HandleBookingConfirmedEvent(
        BookingConfirmedEvent message,
        CancellationToken cancellationToken) =>
        emailSender.SendAsync(
            new BookingConfirmationEmail(
                message.BookingId,
                message.EventId,
                message.UserId,
                message.ConfirmedAt),
            cancellationToken);
}
