using System.Collections.Concurrent;

namespace ModularVerticalSlice.Application.Delivery.BookingConfirmation;

/// <summary>
/// Sends booking-confirmation email requests owned by the delivery boundary.
/// </summary>
public interface IBookingConfirmationEmailSender
{
    /// <summary>
    /// Sends a booking-confirmation email.
    /// </summary>
    Task SendAsync(BookingConfirmationEmail email, CancellationToken cancellationToken);
}

/// <summary>
/// Records confirmation emails in memory for the current implementation.
/// </summary>
public sealed class FakeBookingConfirmationEmailSender : IBookingConfirmationEmailSender
{
    private readonly ConcurrentQueue<BookingConfirmationEmail> _sent = new();
    private readonly ConcurrentDictionary<string, EmailSenderSimulationMode> _modes = new();
    private readonly ConcurrentDictionary<string, int> _attempts = new();

    /// <summary>
    /// Gets a snapshot of sent confirmation emails.
    /// </summary>
    public IReadOnlyCollection<BookingConfirmationEmail> Sent => _sent.ToArray();

    /// <summary>
    /// Gets the delivery-attempt count for a user.
    /// </summary>
    public int AttemptsFor(string userId) =>
        _attempts.TryGetValue(userId, out var attempts) ? attempts : 0;

    /// <summary>
    /// Configures deterministic delivery behavior for a user.
    /// </summary>
    public void Configure(string userId, EmailSenderSimulationMode mode) =>
        _modes[userId] = mode;

    /// <inheritdoc />
    public Task SendAsync(BookingConfirmationEmail email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var attempt = _attempts.AddOrUpdate(email.UserId, 1, (_, current) => current + 1);
        var mode = _modes.GetValueOrDefault(email.UserId, EmailSenderSimulationMode.Success);

        if (mode == EmailSenderSimulationMode.TransientFailure && attempt == 1)
        {
            throw NotificationDeliveryException.Transient();
        }

        if (mode == EmailSenderSimulationMode.PermanentFailure)
        {
            throw NotificationDeliveryException.Permanent();
        }

        _sent.Enqueue(email);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Defines deterministic behavior exposed by the fake email sender.
/// </summary>
public enum EmailSenderSimulationMode
{
    /// <summary>
    /// Delivery succeeds.
    /// </summary>
    Success,

    /// <summary>
    /// The first attempt fails transiently and the next attempt succeeds.
    /// </summary>
    TransientFailure,

    /// <summary>
    /// Every attempt fails permanently.
    /// </summary>
    PermanentFailure
}
