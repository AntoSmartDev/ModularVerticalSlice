using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.SharedKernel;
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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            CreateEligibleMessageBus(),
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 18, 0, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            handler.HandleProcessPayment(
                new ProcessPaymentCommand(
                    Guid.NewGuid(),
                    eventId,
                    "technical-failure-user",
                    1,
                DateTimeOffset.MaxValue),
                TestContext.Current.CancellationToken));

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
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            CreateEligibleMessageBus(),
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 18, 5, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            handler.HandleProcessPayment(
                new ProcessPaymentCommand(
                    Guid.NewGuid(),
                    eventId,
                    "technical-terminal-user",
                    1,
                DateTimeOffset.MaxValue),
                TestContext.Current.CancellationToken));

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
        var bus = CreateEligibleMessageBus();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Business decline event",
            Date = new DateTimeOffset(2026, 6, 20, 12, 0, 0, TimeSpan.Zero),
            TicketPrice = 20m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            bus,
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 18, 10, 0, TimeSpan.Zero)));

        var result = await handler.HandleProcessPayment(
            new ProcessPaymentCommand(
                Guid.NewGuid(),
                eventId,
                "declined-user",
                1,
                DateTimeOffset.MaxValue),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(db.ChangeTracker.Entries<Application.Modules.Payments.Persistence.Entities.Payment>());
        Assert.Contains(bus.Published, x => x is Envelope { Message: PaymentFailedEvent });
    }

    /// <summary>
    /// Verifies that the integrated runtime-retry route keeps the canonical cooldown sequence explicit.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Retryable_Technical_Failure_Should_Preserve_Canonical_Cooldowns()
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Cooldown event",
            Date = new DateTimeOffset(2026, 6, 20, 13, 0, 0, TimeSpan.Zero),
            TicketPrice = 20m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            CreateEligibleMessageBus(),
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 18, 15, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            handler.HandleProcessPayment(
                new ProcessPaymentCommand(
                    Guid.NewGuid(),
                    eventId,
                    "technical-failure-user",
                    1,
                DateTimeOffset.MaxValue),
                TestContext.Current.CancellationToken));

        var route = PaymentsTechnicalFailureRuntimeObservability.Describe(exception);

        Assert.Equal(3, route.Cooldowns.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), route.Cooldowns[0]);
        Assert.Equal(TimeSpan.FromSeconds(5), route.Cooldowns[1]);
        Assert.Equal(TimeSpan.FromSeconds(15), route.Cooldowns[2]);
    }

    /// <summary>
    /// Verifies that the integrated retry and DLQ routes stay explicitly distinct end-to-end.
    /// </summary>
    [Fact]
    public async Task ProcessPayment_Technical_Failure_Routes_Should_Stay_Distinct_End_To_End()
    {
        var retryRoute = await ExecuteAndDescribeTechnicalFailure("technical-failure-user");
        var dlqRoute = await ExecuteAndDescribeTechnicalFailure("technical-terminal-user");

        Assert.NotEqual(retryRoute.PolicyName, dlqRoute.PolicyName);
        Assert.NotEqual(retryRoute.RouteName, dlqRoute.RouteName);
        Assert.NotEmpty(retryRoute.Cooldowns);
        Assert.Empty(dlqRoute.Cooldowns);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static TestMessageContext CreateEligibleMessageBus()
    {
        var bus = new TestMessageContext();
        bus.WhenInvokedMessageOf<CheckBookingPaymentEligibilityQuery>()
            .RespondWith(Result.Success());
        return bus;
    }

    private static async Task<PaymentsTechnicalFailureRuntimeRoute> ExecuteAndDescribeTechnicalFailure(
        string userId)
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Technical route event",
            Date = new DateTimeOffset(2026, 6, 20, 14, 0, 0, TimeSpan.Zero),
            TicketPrice = 20m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = new PaymentProcessingHandler(
            db,
            db,
            new FakePaymentGateway(),
            CreateEligibleMessageBus(),
            new FixedTimeProvider(new DateTimeOffset(2026, 6, 4, 18, 20, 0, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<PaymentTechnicalFailureException>(() =>
            handler.HandleProcessPayment(
                new ProcessPaymentCommand(
                    Guid.NewGuid(),
                    eventId,
                    userId,
                    1,
                DateTimeOffset.MaxValue),
                TestContext.Current.CancellationToken));

        return PaymentsTechnicalFailureRuntimeObservability.Describe(exception);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
