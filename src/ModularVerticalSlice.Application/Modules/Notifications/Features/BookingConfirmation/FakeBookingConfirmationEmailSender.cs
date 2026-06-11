using System.Collections.Concurrent;

namespace ModularVerticalSlice.Application.Modules.Notifications.Features.BookingConfirmation;

/// <summary>
/// Records confirmation emails in memory for the current baseline.
/// </summary>
public sealed class FakeBookingConfirmationEmailSender : IBookingConfirmationEmailSender
{
    private readonly ConcurrentQueue<BookingConfirmationEmail> _sent = new();

    /// <summary>
    /// Gets a snapshot of sent confirmation emails.
    /// </summary>
    public IReadOnlyCollection<BookingConfirmationEmail> Sent => _sent.ToArray();

    /// <inheritdoc />
    public Task SendAsync(BookingConfirmationEmail email, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _sent.Enqueue(email);
        return Task.CompletedTask;
    }
}
