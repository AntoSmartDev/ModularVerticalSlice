namespace ModularVerticalSlice.Application.Modules.Notifications.Features.BookingConfirmation;

/// <summary>
/// Sends booking-confirmation email requests owned by Notifications.
/// </summary>
public interface IBookingConfirmationEmailSender
{
    /// <summary>
    /// Sends a booking-confirmation email.
    /// </summary>
    Task SendAsync(BookingConfirmationEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// Contains the information required to send a booking-confirmation email.
/// </summary>
public sealed record BookingConfirmationEmail(
    Guid BookingId,
    Guid EventId,
    string UserId,
    DateTimeOffset ConfirmedAt);
