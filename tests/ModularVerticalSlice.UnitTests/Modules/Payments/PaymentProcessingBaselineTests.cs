using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;
using ModularVerticalSlice.Persistence;
using Wolverine;

namespace ModularVerticalSlice.UnitTests.Modules.Payments;

/// <summary>
/// Verifies the baseline Payments processing behavior.
/// </summary>
public class PaymentProcessingBaselineTests
{
    /// <summary>
    /// Verifies that a successful payment creates a succeeded payment record and publishes the success event.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Should_Persist_Succeeded_Payment_And_Publish_Success_Event()
    {
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var processedAt = new DateTimeOffset(2026, 5, 23, 22, 0, 0, TimeSpan.Zero);

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "OpenAI Conf",
            Date = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 49.90m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            bus,
            new FixedTimeProvider(processedAt));

        var result = await handler.Handle(
            new ProcessPaymentCommand(
                bookingId,
                eventId,
                "user-1",
                2),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(db.ChangeTracker.Entries<Payment>());

        var payment = db.ChangeTracker.Entries<Payment>().Single().Entity;
        Assert.Equal(bookingId, payment.BookingId);
        Assert.Equal(99.80m, payment.Amount);
        Assert.Equal(PaymentStatus.Succeeded, payment.Status);
        Assert.Equal(processedAt, payment.CreatedAt);
        Assert.Equal(processedAt, payment.CompletedAt);

        Assert.Contains(bus.Published, x =>
            x is Envelope { Message: PaymentSucceededEvent published } &&
            published.BookingId == bookingId &&
            published.PaymentId == payment.Id &&
            published.PaidAt == processedAt);
    }

    /// <summary>
    /// Verifies that a business decline creates a failed payment record and publishes the failure event.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Should_Persist_Failed_Payment_And_Publish_Failure_Event()
    {
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var processedAt = new DateTimeOffset(2026, 5, 23, 22, 5, 0, TimeSpan.Zero);

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Declined event",
            Date = new DateTimeOffset(2026, 6, 11, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 20m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            bus,
            new FixedTimeProvider(processedAt));

        var result = await handler.Handle(
            new ProcessPaymentCommand(
                bookingId,
                eventId,
                "declined-user",
                3),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(db.ChangeTracker.Entries<Payment>());

        var payment = db.ChangeTracker.Entries<Payment>().Single().Entity;
        Assert.Equal(bookingId, payment.BookingId);
        Assert.Equal(60m, payment.Amount);
        Assert.Equal(PaymentStatus.Failed, payment.Status);
        Assert.Equal(processedAt, payment.CompletedAt);

        Assert.Contains(bus.Published, x =>
            x is Envelope { Message: PaymentFailedEvent published } &&
            published.BookingId == bookingId &&
            published.PaymentId == payment.Id &&
            published.Reason == "Payment was declined." &&
            published.FailedAt == processedAt);
    }

    /// <summary>
    /// Verifies that payment processing fails when the related event does not exist.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Should_Fail_When_Event_Does_Not_Exist()
    {
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();
        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            bus,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 23, 22, 10, 0, TimeSpan.Zero)));

        var result = await handler.Handle(
            new ProcessPaymentCommand(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "user-1",
                1),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Catalog.EventNotFound", result.Error.Code);
        Assert.Empty(db.ChangeTracker.Entries<Payment>());
        Assert.Empty(bus.Published);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
