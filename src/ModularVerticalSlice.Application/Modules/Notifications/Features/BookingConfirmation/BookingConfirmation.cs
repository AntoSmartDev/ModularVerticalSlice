using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Notifications.Features.BookingConfirmation;

/// <summary>
/// Contains the information required to send a booking-confirmation email.
/// </summary>
public sealed record BookingConfirmationEmail(
    Guid BookingId,
    Guid EventId,
    string UserId,
    DateTimeOffset ConfirmedAt);

/// <summary>
/// Handles booking confirmation notifications.
/// </summary>
public sealed class BookingConfirmationHandler(IBookingConfirmationEmailSender emailSender)
{
    /// <summary>
    /// Sends the confirmation email represented by a confirmed booking event.
    /// </summary>
    [Idempotent]
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
