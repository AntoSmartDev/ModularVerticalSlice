using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Persistence;
using Wolverine;

namespace ModularVerticalSlice.UnitTests.Modules.Payments;

/// <summary>
/// Verifies the first integration-level baseline for Payments runtime retry and DLQ handling.
/// </summary>
public class PaymentsRuntimeRecoveryIntegrationBaselineTests
{
    /// <summary>
    /// Verifies that a retryable technical failure raised by the real handler maps to the runtime-retry route.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Retryable_Technical_Failure_Should_Map_To_Runtime_Retry_Baseline()
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Runtime retry event",
            Date = new DateTimeOffset(2026, 6, 20, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 20m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            new TestMessageContext(),
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 18, 0, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            handler.Handle(
                new ProcessPaymentCommand(
                    Guid.NewGuid(),
                    eventId,
                    "technical-failure-user",
                    1),
                CancellationToken.None));

        var route = PaymentsTechnicalFailureRuntimeObservability.Describe(exception);

        Assert.Equal(PaymentsTechnicalFailureRuntimeObservability.RuntimeManagedRecoveryPolicyName, route.PolicyName);
        Assert.Equal(PaymentsTechnicalFailureRuntimeObservability.RuntimeRetryRoute, route.RouteName);
        Assert.True(route.UsesRuntimeRetry);
        Assert.False(route.UsesErrorQueue);
        Assert.Equal(
            PaymentsTechnicalFailureRuntimeObservability.RuntimeRecoveryCooldowns,
            route.Cooldowns);
    }

    /// <summary>
    /// Verifies that a terminal technical failure raised by the real handler maps to the DLQ route.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Terminal_Technical_Failure_Should_Map_To_Dlq_Baseline()
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "DLQ event",
            Date = new DateTimeOffset(2026, 6, 20, 11, 0, 0, TimeSpan.Zero),
            TicketPrice = 20m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            new TestMessageContext(),
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 18, 5, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            handler.Handle(
                new ProcessPaymentCommand(
                    Guid.NewGuid(),
                    eventId,
                    "technical-terminal-user",
                    1),
                CancellationToken.None));

        var route = PaymentsTechnicalFailureRuntimeObservability.Describe(exception);

        Assert.Equal(PaymentsTechnicalFailureRuntimeObservability.EscalationToDlqPolicyName, route.PolicyName);
        Assert.Equal(PaymentsTechnicalFailureRuntimeObservability.ErrorQueueRoute, route.RouteName);
        Assert.False(route.UsesRuntimeRetry);
        Assert.True(route.UsesErrorQueue);
        Assert.Empty(route.Cooldowns);
    }

    /// <summary>
    /// Verifies that a business failure stays outside the runtime retry and DLQ integration story.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Business_Failure_Should_Stay_Outside_Runtime_Retry_And_Dlq_Baseline()
    {
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Business decline event",
            Date = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            TicketPrice = 20m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            bus,
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 18, 10, 0, TimeSpan.Zero)));

        var result = await handler.Handle(
            new ProcessPaymentCommand(
                Guid.NewGuid(),
                eventId,
                "declined-user",
                1),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(db.ChangeTracker.Entries<Application.Modules.Payments.Persistence.Entities.Payment>());
        Assert.Contains(bus.Published, x => x is Envelope { Message: PaymentFailedEvent });
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
