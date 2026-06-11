using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Notifications;
using ModularVerticalSlice.Application.Modules.Notifications.Features.BookingConfirmation;

namespace ModularVerticalSlice.UnitTests.Modules.Notifications;

/// <summary>
/// Verifies the baseline booking-confirmation notification behavior.
/// </summary>
public sealed class BookingConfirmationBaselineTests
{
    [Fact]
    public async Task BookingConfirmedEvent_Should_Send_Mapped_Confirmation_Email()
    {
        var sender = new FakeBookingConfirmationEmailSender();
        var handler = new BookingConfirmationHandler(sender);
        var message = new BookingConfirmedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-1",
            new DateTimeOffset(2026, 6, 11, 11, 0, 0, TimeSpan.Zero));

        await handler.HandleBookingConfirmedEvent(message, TestContext.Current.CancellationToken);

        var email = Assert.Single(sender.Sent);
        Assert.Equal(message.BookingId, email.BookingId);
        Assert.Equal(message.EventId, email.EventId);
        Assert.Equal(message.UserId, email.UserId);
        Assert.Equal(message.ConfirmedAt, email.ConfirmedAt);
    }

    [Fact]
    public void NotificationsModule_Should_Register_Observable_Fake_Email_Sender()
    {
        var services = new ServiceCollection();
        new NotificationsModule().RegisterModule(services, configuration: null!);

        using var provider = services.BuildServiceProvider();

        var abstraction = provider.GetRequiredService<IBookingConfirmationEmailSender>();
        var concrete = provider.GetRequiredService<FakeBookingConfirmationEmailSender>();
        Assert.Same(concrete, abstraction);
    }
}
