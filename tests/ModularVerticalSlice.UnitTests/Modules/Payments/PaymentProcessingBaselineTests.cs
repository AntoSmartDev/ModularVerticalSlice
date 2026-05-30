using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments.Domain;
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
    /// Verifies that the policy maps a missing owner to an explicit business decline.
    /// </summary>
    [Fact]
    public void PaymentOutcomePolicy_Should_Return_BusinessDecline_For_Missing_User()
    {
        var outcome = PaymentOutcomePolicy.Decide("", 1);

        Assert.False(outcome.IsSuccess);
        Assert.True(outcome.IsBusinessFailure);
        Assert.False(outcome.IsTechnicalFailure);
        Assert.Equal("Missing payment owner.", outcome.FailureReason);
    }

    /// <summary>
    /// Verifies that the policy maps an invalid quantity to an explicit business decline.
    /// </summary>
    [Fact]
    public void PaymentOutcomePolicy_Should_Return_BusinessDecline_For_Invalid_Quantity()
    {
        var outcome = PaymentOutcomePolicy.Decide("user-1", 0);

        Assert.False(outcome.IsSuccess);
        Assert.True(outcome.IsBusinessFailure);
        Assert.False(outcome.IsTechnicalFailure);
        Assert.Equal("Invalid payment quantity.", outcome.FailureReason);
    }

    /// <summary>
    /// Verifies that the explicit business-decline factory preserves business-failure semantics.
    /// </summary>
    [Fact]
    public void PaymentOutcomeDecision_BusinessDecline_Should_Set_Business_Semantics()
    {
        var outcome = PaymentOutcomeDecision.BusinessDecline("declined");

        Assert.False(outcome.IsSuccess);
        Assert.True(outcome.IsBusinessFailure);
        Assert.False(outcome.IsTechnicalFailure);
        Assert.False(outcome.IsRetriableTechnicalFailure);
        Assert.False(outcome.IsRecoverableProviderState);
        Assert.False(outcome.IsTerminalProviderState);
        Assert.Null(outcome.ProviderState);
        Assert.Equal("declined", outcome.FailureReason);
    }

    /// <summary>
    /// Verifies that the explicit retriable technical-failure factory preserves retryable technical semantics.
    /// </summary>
    [Fact]
    public void PaymentOutcomeDecision_RetriableTechnicalFailure_Should_Set_Technical_Semantics()
    {
        var outcome = PaymentOutcomeDecision.RetriableTechnicalFailure("temporary");

        Assert.False(outcome.IsSuccess);
        Assert.False(outcome.IsBusinessFailure);
        Assert.True(outcome.IsTechnicalFailure);
        Assert.True(outcome.IsRetriableTechnicalFailure);
        Assert.True(outcome.IsRecoverableProviderState);
        Assert.False(outcome.IsTerminalProviderState);
        Assert.Equal(PaymentProviderStateKind.DegradedRecoverable, outcome.ProviderState);
        Assert.Equal("temporary", outcome.FailureReason);
    }

    /// <summary>
    /// Verifies that the explicit non-retriable technical-failure factory preserves terminal technical semantics.
    /// </summary>
    [Fact]
    public void PaymentOutcomeDecision_NonRetriableTechnicalFailure_Should_Set_Terminal_Technical_Semantics()
    {
        var outcome = PaymentOutcomeDecision.NonRetriableTechnicalFailure("terminal");

        Assert.False(outcome.IsSuccess);
        Assert.False(outcome.IsBusinessFailure);
        Assert.True(outcome.IsTechnicalFailure);
        Assert.False(outcome.IsRetriableTechnicalFailure);
        Assert.False(outcome.IsRecoverableProviderState);
        Assert.True(outcome.IsTerminalProviderState);
        Assert.Equal(PaymentProviderStateKind.Terminal, outcome.ProviderState);
        Assert.Equal("terminal", outcome.FailureReason);
    }

    /// <summary>
    /// Verifies that the retriable technical-failure factory preserves the explicit retry shape.
    /// </summary>
    [Fact]
    public void PaymentTechnicalFailureException_Retriable_Should_Set_Retry_Shape()
    {
        var exception = PaymentTechnicalFailureException.DegradedRecoverable("temporary");

        Assert.Equal("temporary", exception.Message);
        Assert.True(exception.IsRetriable);
        Assert.Equal(PaymentProviderStateKind.DegradedRecoverable, exception.ProviderState);
    }

    /// <summary>
    /// Verifies that the non-retriable technical-failure factory preserves the explicit terminal shape.
    /// </summary>
    [Fact]
    public void PaymentTechnicalFailureException_NonRetriable_Should_Set_Terminal_Shape()
    {
        var exception = PaymentTechnicalFailureException.Terminal("terminal");

        Assert.Equal("terminal", exception.Message);
        Assert.False(exception.IsRetriable);
        Assert.Equal(PaymentProviderStateKind.Terminal, exception.ProviderState);
    }

    /// <summary>
    /// Verifies that provider-state semantics remain absent from pure business failures.
    /// </summary>
    [Fact]
    public void PaymentOutcomeDecision_BusinessDecline_Should_Not_Carry_Provider_State()
    {
        var outcome = PaymentOutcomePolicy.Decide("declined-user", 1);

        Assert.True(outcome.IsBusinessFailure);
        Assert.False(outcome.IsTechnicalFailure);
        Assert.Null(outcome.ProviderState);
        Assert.False(outcome.IsRecoverableProviderState);
        Assert.False(outcome.IsTerminalProviderState);
    }

    /// <summary>
    /// Verifies that retryability and provider-state semantics stay explicit together on technical failures.
    /// </summary>
    [Fact]
    public void PaymentTechnicalFailureException_DegradedRecoverable_Should_Carry_Provider_State_And_Retryability()
    {
        var exception = PaymentTechnicalFailureException.DegradedRecoverable("temporary");

        Assert.True(exception.IsRetriable);
        Assert.Equal(PaymentProviderStateKind.DegradedRecoverable, exception.ProviderState);
    }

    /// <summary>
    /// Verifies that the fake gateway returns a successful decision for the baseline happy path.
    /// </summary>
    [Fact]
    public void FakePaymentGateway_Should_Return_Success_For_Baseline_Happy_Path()
    {
        var gateway = new FakePaymentGateway();

        var outcome = gateway.Process("user-1", 2);

        Assert.True(outcome.IsSuccess);
        Assert.False(outcome.IsBusinessFailure);
        Assert.False(outcome.IsTechnicalFailure);
        Assert.Null(outcome.FailureReason);
    }

    /// <summary>
    /// Verifies that the fake gateway returns a deterministic decline for the baseline business-failure path.
    /// </summary>
    [Fact]
    public void FakePaymentGateway_Should_Return_Failure_For_Deterministic_Decline_Path()
    {
        var gateway = new FakePaymentGateway();

        var outcome = gateway.Process("declined-user", 2);

        Assert.False(outcome.IsSuccess);
        Assert.True(outcome.IsBusinessFailure);
        Assert.False(outcome.IsTechnicalFailure);
        Assert.Equal("Payment was declined.", outcome.FailureReason);
    }

    /// <summary>
    /// Verifies that the fake gateway can produce a deterministic technical-failure outcome.
    /// </summary>
    [Fact]
    public void FakePaymentGateway_Should_Return_Technical_Failure_For_Deterministic_Technical_Path()
    {
        var gateway = new FakePaymentGateway();

        var outcome = gateway.Process("technical-failure-user", 2);

        Assert.False(outcome.IsSuccess);
        Assert.False(outcome.IsBusinessFailure);
        Assert.True(outcome.IsTechnicalFailure);
        Assert.True(outcome.IsRetriableTechnicalFailure);
        Assert.True(outcome.IsRecoverableProviderState);
        Assert.Equal(PaymentProviderStateKind.DegradedRecoverable, outcome.ProviderState);
        Assert.Equal("Payment provider is temporarily unavailable.", outcome.FailureReason);
    }

    /// <summary>
    /// Verifies that the fake gateway can produce a deterministic non-retriable technical-failure outcome.
    /// </summary>
    [Fact]
    public void FakePaymentGateway_Should_Return_NonRetriable_Technical_Failure_For_Terminal_Technical_Path()
    {
        var gateway = new FakePaymentGateway();

        var outcome = gateway.Process("technical-terminal-user", 2);

        Assert.False(outcome.IsSuccess);
        Assert.False(outcome.IsBusinessFailure);
        Assert.True(outcome.IsTechnicalFailure);
        Assert.False(outcome.IsRetriableTechnicalFailure);
        Assert.True(outcome.IsTerminalProviderState);
        Assert.Equal(PaymentProviderStateKind.Terminal, outcome.ProviderState);
        Assert.Equal("Payment provider rejected the request as non-retriable.", outcome.FailureReason);
    }

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
    /// Verifies that a technical gateway failure is surfaced as an exception so runtime retry semantics can own it.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Should_Throw_Dedicated_Technical_Failure_Without_Persisting_Or_Publishing()
    {
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            bus,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.Zero)));

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Technical failure event",
            Date = new DateTimeOffset(2026, 6, 12, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 30m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            handler.Handle(
                new ProcessPaymentCommand(
                    bookingId,
                    eventId,
                    "technical-failure-user",
                    1),
                CancellationToken.None));

        Assert.Equal("Payment provider is temporarily unavailable.", exception.Message);
        Assert.True(exception.IsRetriable);
        Assert.Equal(PaymentProviderStateKind.DegradedRecoverable, exception.ProviderState);
        Assert.Empty(db.ChangeTracker.Entries<Payment>());
        Assert.Empty(bus.Published);
    }

    /// <summary>
    /// Verifies that non-retriable technical failures are still surfaced explicitly with their retry shape.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Should_Throw_NonRetriable_Technical_Failure_With_Explicit_Shape()
    {
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();
        var eventId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            bus,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 28, 20, 10, 0, TimeSpan.Zero)));

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Terminal technical failure event",
            Date = new DateTimeOffset(2026, 6, 12, 11, 0, 0, TimeSpan.Zero),
            TicketPrice = 30m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            handler.Handle(
                new ProcessPaymentCommand(
                    bookingId,
                    eventId,
                    "technical-terminal-user",
                    1),
                CancellationToken.None));

        Assert.Equal("Payment provider rejected the request as non-retriable.", exception.Message);
        Assert.False(exception.IsRetriable);
        Assert.Equal(PaymentProviderStateKind.Terminal, exception.ProviderState);
        Assert.Empty(db.ChangeTracker.Entries<Payment>());
        Assert.Empty(bus.Published);
    }

    /// <summary>
    /// Verifies that business failure and technical failure remain clearly separated in their observable effects.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Should_Separate_Business_And_Technical_Failure_Effects()
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Separation event",
            Date = new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 25m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var businessBus = new TestMessageContext();
        var businessHandler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            businessBus,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.Zero)));

        var businessResult = await businessHandler.Handle(
            new ProcessPaymentCommand(
                Guid.NewGuid(),
                eventId,
                "declined-user",
                1),
            CancellationToken.None);

        Assert.True(businessResult.IsSuccess);
        Assert.Contains(businessBus.Published, x => x is Envelope { Message: PaymentFailedEvent });
        Assert.Single(db.ChangeTracker.Entries<Payment>());

        await using var technicalDb = CreateDbContext();
        technicalDb.Events.Add(new Event
        {
            Id = eventId,
            Title = "Separation event",
            Date = new DateTimeOffset(2026, 6, 13, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 25m,
            AvailableTickets = 10
        });
        await technicalDb.SaveChangesAsync();

        var technicalBus = new TestMessageContext();
        var technicalHandler = new PaymentProcessingHandler(
            technicalDb,
            technicalDb,
            new FakePaymentGateway(),
            technicalBus,
            new FixedTimeProvider(new DateTimeOffset(2026, 5, 28, 21, 5, 0, TimeSpan.Zero)));

        await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            technicalHandler.Handle(
                new ProcessPaymentCommand(
                    Guid.NewGuid(),
                    eventId,
                    "technical-failure-user",
                    1),
                CancellationToken.None));

        Assert.Empty(technicalBus.Published);
        Assert.Empty(technicalDb.ChangeTracker.Entries<Payment>());
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
