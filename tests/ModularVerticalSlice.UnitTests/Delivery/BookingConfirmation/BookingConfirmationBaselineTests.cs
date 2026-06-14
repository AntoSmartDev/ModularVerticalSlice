using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Delivery.BookingConfirmation;

namespace ModularVerticalSlice.UnitTests.Delivery.BookingConfirmation;

/// <summary>
/// Verifies the baseline booking-confirmation delivery behavior.
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
    public void DeliveryModule_Should_Register_Observable_Fake_Email_Sender()
    {
        var services = new ServiceCollection();
        new BookingConfirmationDeliveryModule().RegisterServices(services, configuration: null!);

        using var provider = services.BuildServiceProvider();

        var abstraction = provider.GetRequiredService<IBookingConfirmationEmailSender>();
        var concrete = provider.GetRequiredService<FakeBookingConfirmationEmailSender>();
        Assert.Same(concrete, abstraction);
    }

    [Fact]
    public async Task FakeSender_Should_Fail_Transiently_Once_Then_Succeed()
    {
        var sender = new FakeBookingConfirmationEmailSender();
        var email = new BookingConfirmationEmail(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "transient-user",
            DateTimeOffset.UtcNow);
        sender.Configure(email.UserId, EmailSenderSimulationMode.TransientFailure);

        await Assert.ThrowsAsync<NotificationDeliveryException>(() =>
            sender.SendAsync(email, TestContext.Current.CancellationToken));
        await sender.SendAsync(email, TestContext.Current.CancellationToken);

        Assert.Single(sender.Sent);
        Assert.Equal(2, sender.AttemptsFor(email.UserId));
    }

    [Fact]
    public async Task FakeSender_Should_Keep_Failing_Permanently()
    {
        var sender = new FakeBookingConfirmationEmailSender();
        var email = new BookingConfirmationEmail(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "permanent-user",
            DateTimeOffset.UtcNow);
        sender.Configure(email.UserId, EmailSenderSimulationMode.PermanentFailure);

        var exception = await Assert.ThrowsAsync<NotificationDeliveryException>(() =>
            sender.SendAsync(email, TestContext.Current.CancellationToken));

        Assert.False(exception.IsTransient);
        Assert.Empty(sender.Sent);
    }
}
