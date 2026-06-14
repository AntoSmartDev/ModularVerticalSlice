using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Delivery.BookingConfirmation;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Modules.Payments.Domain;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Modules;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi;
using Npgsql;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Tracking;
using Wolverine.Transports;

namespace ModularVerticalSlice.IntegrationTests.Payments;

[Collection("Payments runtime resilience")]
public sealed class PaymentsRuntimeResilienceIntegrationTests
{
    [Fact]
    public async Task Retriable_Technical_Failure_Should_Use_Wolverine_Retry()
    {
        using var host = await StartHostAsync();
        var gateway = host.Services.GetRequiredService<ObservablePaymentGateway>();
        var scenario = NewScenario("retry");
        gateway.Configure(scenario.UserId, PaymentGatewaySimulation.TransientThenSuccess);
        await SeedEligibleScenarioAsync(host, scenario);

        var session = await host
            .TrackActivity()
            .SendMessageAndWaitAsync(scenario.Command, new DeliveryOptions());

        Assert.Empty(session.AllExceptions());
        Assert.Equal(2, gateway.AttemptsFor(scenario.UserId));
        var attemptTimes = gateway.AttemptTimesFor(scenario.UserId);
        Assert.True(
            attemptTimes[1] - attemptTimes[0] >= TimeSpan.FromMilliseconds(800),
            "The retry did not observe the configured first Wolverine cooldown.");
        Assert.Equal(PaymentStatus.Succeeded, await ReadPaymentStatusAsync(host, scenario.BookingId));
    }

    [Fact]
    public async Task Terminal_Technical_Failure_Should_Reach_Error_Queue()
    {
        using var host = await StartHostAsync();
        var gateway = host.Services.GetRequiredService<ObservablePaymentGateway>();
        var scenario = NewScenario("terminal");
        gateway.Configure(scenario.UserId, PaymentGatewaySimulation.Terminal);
        await SeedEligibleScenarioAsync(host, scenario);

        var session = await host
            .TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .SendMessageAndWaitAsync(scenario.Command, new DeliveryOptions());

        Assert.NotEmpty(session.AllRecordsInOrder(MessageEventType.MovedToErrorQueue));
        Assert.Equal(1, gateway.AttemptsFor(scenario.UserId));
        Assert.Null(await ReadPaymentStatusAsync(host, scenario.BookingId));
    }

    [Fact]
    public async Task Business_Decline_Should_Complete_Without_Retry_Or_Error_Queue()
    {
        using var host = await StartHostAsync();
        var gateway = host.Services.GetRequiredService<ObservablePaymentGateway>();
        var scenario = NewScenario("decline");
        var secondDecline = NewScenario("decline-second");
        var probe = NewScenario("decline-probe");
        gateway.Configure(scenario.UserId, PaymentGatewaySimulation.BusinessDecline);
        gateway.Configure(secondDecline.UserId, PaymentGatewaySimulation.BusinessDecline);
        gateway.Configure(probe.UserId, PaymentGatewaySimulation.Success);
        await SeedEligibleScenarioAsync(host, scenario);
        await SeedEligibleScenarioAsync(host, secondDecline);
        await SeedEligibleScenarioAsync(host, probe);

        var session = await host
            .TrackActivity()
            .SendMessageAndWaitAsync(scenario.Command, new DeliveryOptions());
        var secondSession = await host
            .TrackActivity()
            .SendMessageAndWaitAsync(secondDecline.Command, new DeliveryOptions());
        await SendAsync(host, probe.Command);
        await WaitUntilAsync(() => gateway.AttemptsFor(probe.UserId) == 1, TimeSpan.FromSeconds(2));

        Assert.Empty(session.AllExceptions());
        Assert.Empty(secondSession.AllExceptions());
        Assert.Empty(session.AllRecordsInOrder(MessageEventType.MovedToErrorQueue));
        Assert.Equal(1, gateway.AttemptsFor(scenario.UserId));
        Assert.Equal(1, gateway.AttemptsFor(secondDecline.UserId));
        Assert.Equal(PaymentStatus.Failed, await ReadPaymentStatusAsync(host, scenario.BookingId));
        Assert.Contains(session.Sent.MessagesOf<PaymentFailedEvent>(), x => x.BookingId == scenario.BookingId);
    }

    [Fact]
    public async Task Breaker_Should_Pause_Only_Payments_Queue_And_Resume_After_Pause()
    {
        using var host = await StartHostAsync();
        var gateway = host.Services.GetRequiredService<ObservablePaymentGateway>();
        var emailSender = host.Services.GetRequiredService<FakeBookingConfirmationEmailSender>();
        var listener = (IListenerCircuit)host.Services
            .GetRequiredService<IWolverineRuntime>()
            .Endpoints
            .AgentForLocalQueue(PaymentsCircuitBreakerOptions.QueueName);
        var scenario = NewScenario("breaker");
        var secondFailure = NewScenario("breaker-second-failure");
        var pausedProbe = NewScenario("breaker-paused-probe");
        var resumedProbe = NewScenario("breaker-resumed-probe");
        gateway.Configure(scenario.UserId, PaymentGatewaySimulation.AlwaysDegradedRecoverable);
        gateway.Configure(secondFailure.UserId, PaymentGatewaySimulation.AlwaysDegradedRecoverable);
        gateway.Configure(pausedProbe.UserId, PaymentGatewaySimulation.Success);
        gateway.Configure(resumedProbe.UserId, PaymentGatewaySimulation.Success);
        await SeedEligibleScenarioAsync(host, scenario);
        await SeedEligibleScenarioAsync(host, secondFailure);
        await SeedEligibleScenarioAsync(host, pausedProbe);
        await SeedEligibleScenarioAsync(host, resumedProbe);

        await SendAsync(host, scenario.Command);
        await SendAsync(host, secondFailure.Command);
        await WaitUntilAsync(
            () => gateway.AttemptsFor(scenario.UserId) >= 4
                  && gateway.AttemptsFor(secondFailure.UserId) >= 4,
            TimeSpan.FromSeconds(30));
        await WaitUntilAsync(() => listener.Status == ListeningStatus.TooBusy, TimeSpan.FromSeconds(5));

        var attemptsWhilePaused = gateway.TotalAttemptsFor(scenario.UserId, secondFailure.UserId);
        await SendAsync(host, pausedProbe.Command);
        await AssertRemainsAsync(
            () => gateway.TotalAttemptsFor(scenario.UserId, secondFailure.UserId, pausedProbe.UserId),
            attemptsWhilePaused,
            TimeSpan.FromSeconds(1));

        var notification = new BookingConfirmedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            $"unrelated-{Guid.NewGuid():N}",
            DateTimeOffset.UtcNow);
        await SendAsync(host, notification);
        await WaitUntilAsync(() => emailSender.AttemptsFor(notification.UserId) == 1, TimeSpan.FromSeconds(5));

        await WaitUntilAsync(() => listener.Status == ListeningStatus.Accepting, TimeSpan.FromSeconds(10));
        await SendAsync(host, resumedProbe.Command);
        await WaitUntilAsync(() => gateway.AttemptsFor(resumedProbe.UserId) == 1, TimeSpan.FromSeconds(5));

        Assert.Equal(1, emailSender.AttemptsFor(notification.UserId));
        Assert.Equal(PaymentStatus.Succeeded, await ReadPaymentStatusAsync(host, resumedProbe.BookingId));

        await ClearMessagingEnvelopeStateAsync(
            host.Services.GetRequiredService<IConfiguration>());
    }

    [Theory]
    [InlineData(BookingStatus.Expired)]
    [InlineData(BookingStatus.Cancelled)]
    [InlineData(BookingStatus.Confirmed)]
    public async Task Ineligible_Booking_Should_Reject_Payment_Without_Side_Effects(BookingStatus status)
    {
        using var host = await StartHostAsync();
        var gateway = host.Services.GetRequiredService<ObservablePaymentGateway>();
        var scenario = NewScenario("late");
        gateway.Configure(scenario.UserId, PaymentGatewaySimulation.Success);
        await SeedEventAsync(host, scenario.EventId);
        await SeedBookingAsync(host, scenario, status);

        var session = await host
            .TrackActivity()
            .SendMessageAndWaitAsync(scenario.Command, new DeliveryOptions());

        Assert.Empty(session.AllExceptions());
        Assert.Equal(0, gateway.AttemptsFor(scenario.UserId));
        Assert.Equal(status, await ReadBookingStatusAsync(host, scenario.BookingId));
        Assert.Null(await ReadPaymentStatusAsync(host, scenario.BookingId));
        Assert.Empty(session.Sent.MessagesOf<PaymentSucceededEvent>());
        Assert.Empty(session.Sent.MessagesOf<PaymentFailedEvent>());
    }

    [Fact]
    public async Task Missing_Booking_Should_Reject_Payment_Without_Side_Effects()
    {
        using var host = await StartHostAsync();
        var gateway = host.Services.GetRequiredService<ObservablePaymentGateway>();
        var scenario = NewScenario("missing");
        gateway.Configure(scenario.UserId, PaymentGatewaySimulation.Success);
        await SeedEventAsync(host, scenario.EventId);

        var session = await host.TrackActivity().SendMessageAndWaitAsync(scenario.Command, new DeliveryOptions());

        Assert.Empty(session.AllExceptions());
        Assert.Equal(0, gateway.AttemptsFor(scenario.UserId));
        Assert.Null(await ReadPaymentStatusAsync(host, scenario.BookingId));
        Assert.Empty(session.Sent.MessagesOf<PaymentSucceededEvent>());
        Assert.Empty(session.Sent.MessagesOf<PaymentFailedEvent>());
    }

    [Fact]
    public async Task Elapsed_Deadline_Should_Reject_Pending_Booking_Without_Side_Effects()
    {
        using var host = await StartHostAsync();
        var gateway = host.Services.GetRequiredService<ObservablePaymentGateway>();
        var scenario = NewScenario("elapsed");
        gateway.Configure(scenario.UserId, PaymentGatewaySimulation.Success);
        await SeedEligibleScenarioAsync(host, scenario);

        var command = scenario.Command with { PaymentDeadline = DateTimeOffset.UtcNow.AddSeconds(-1) };
        var session = await host.TrackActivity().SendMessageAndWaitAsync(command, new DeliveryOptions());

        Assert.Empty(session.AllExceptions());
        Assert.Equal(0, gateway.AttemptsFor(scenario.UserId));
        Assert.Null(await ReadPaymentStatusAsync(host, scenario.BookingId));
        Assert.Empty(session.Sent.MessagesOf<PaymentSucceededEvent>());
        Assert.Empty(session.Sent.MessagesOf<PaymentFailedEvent>());
    }

    private static async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.Development.json", optional: false);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bookings:Lifecycle:PaymentWindow"] = "00:00:10",
            ["Payments:CircuitBreaker:MinimumThreshold"] = "2",
            ["Payments:CircuitBreaker:FailurePercentageThreshold"] = "50",
            ["Payments:CircuitBreaker:TrackingPeriod"] = "00:00:02",
            ["Payments:CircuitBreaker:PauseTime"] = "00:00:03"
        });

        IModule[] modules = [new BookingsModule(), new PaymentsModule(), new BookingConfirmationDeliveryModule()];
        builder.Services.AddApplicationModules(builder.Configuration, modules);
        builder.Services.AddPersistence();
        builder.Services.AddSingleton<ObservablePaymentGateway>();
        builder.Services.AddSingleton<IPaymentGateway>(services =>
            services.GetRequiredService<ObservablePaymentGateway>());
        builder.UseWolverine(options => options.ConfigureApplicationMessaging(builder.Configuration));

        var host = builder.Build();
        await ClearMessagingEnvelopeStateAsync(builder.Configuration);
        await host.StartAsync();

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        return host;
    }

    private static async Task ClearMessagingEnvelopeStateAsync(IConfiguration configuration)
    {
        await using var connection = new NpgsqlConnection(
            configuration.GetConnectionString("Database"));
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var findTables = connection.CreateCommand();
        findTables.CommandText = """
            select format('%I.%I', schemaname, tablename)
            from pg_tables
            where schemaname = 'messaging'
              and (tablename like '%envelope%' or tablename like '%dead_letter%')
            """;

        var tables = new List<string>();
        await using (var reader = await findTables.ExecuteReaderAsync(TestContext.Current.CancellationToken))
        {
            while (await reader.ReadAsync(TestContext.Current.CancellationToken))
                tables.Add(reader.GetString(0));
        }

        if (tables.Count == 0)
            return;

        await using var truncate = connection.CreateCommand();
        truncate.CommandText = $"truncate table {string.Join(", ", tables)} cascade";
        await truncate.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static PaymentScenario NewScenario(string name)
    {
        var id = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var userId = $"{name}-{id:N}";
        return new PaymentScenario(
            id,
            eventId,
            userId,
            new ProcessPaymentCommand(id, eventId, userId, 1, DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    private static async Task SendAsync(IHost host, object message)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().SendAsync(message);
    }

    private static async Task SeedEventAsync(IHost host, Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Payments runtime resilience event",
            Date = DateTimeOffset.UtcNow.AddDays(1),
            TicketPrice = 25m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedEligibleScenarioAsync(IHost host, PaymentScenario scenario)
    {
        await SeedEventAsync(host, scenario.EventId);
        await SeedBookingAsync(host, scenario, BookingStatus.Pending);
    }

    private static async Task SeedBookingAsync(IHost host, PaymentScenario scenario, BookingStatus status)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Bookings.Add(new Booking
        {
            Id = scenario.BookingId,
            EventId = scenario.EventId,
            Quantity = 1,
            Status = status,
            UserId = scenario.UserId,
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();
    }

    private static async Task<PaymentStatus?> ReadPaymentStatusAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Payments
            .Where(x => x.BookingId == bookingId)
            .Select(x => (PaymentStatus?)x.Status)
            .SingleOrDefaultAsync();
    }

    private static async Task<BookingStatus?> ReadBookingStatusAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings
            .Where(x => x.Id == bookingId)
            .Select(x => (BookingStatus?)x.Status)
            .SingleOrDefaultAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), $"Condition was not met within {timeout}.");
    }

    private static async Task AssertRemainsAsync(Func<int> read, int expected, TimeSpan duration)
    {
        var deadline = DateTimeOffset.UtcNow + duration;
        while (DateTimeOffset.UtcNow < deadline)
        {
            Assert.Equal(expected, read());
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }
    }

    private sealed record PaymentScenario(
        Guid BookingId,
        Guid EventId,
        string UserId,
        ProcessPaymentCommand Command);
}

[CollectionDefinition("Payments runtime resilience", DisableParallelization = true)]
public sealed class PaymentsRuntimeResilienceCollection;

public sealed class ObservablePaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, PaymentGatewaySimulation> _simulations = new();
    private readonly ConcurrentDictionary<string, int> _attempts = new();
    private readonly ConcurrentDictionary<string, ConcurrentQueue<DateTimeOffset>> _attemptTimes = new();

    public void Configure(string userId, PaymentGatewaySimulation simulation) =>
        _simulations[userId] = simulation;

    public int AttemptsFor(string userId) =>
        _attempts.GetValueOrDefault(userId);

    public int TotalAttemptsFor(params string[] userIds) =>
        userIds.Sum(AttemptsFor);

    public IReadOnlyList<DateTimeOffset> AttemptTimesFor(string userId) =>
        _attemptTimes.GetValueOrDefault(userId)?.ToArray() ?? [];

    public PaymentOutcomeDecision Process(string userId, int quantity)
    {
        var attempt = _attempts.AddOrUpdate(userId, 1, (_, current) => current + 1);
        _attemptTimes.GetOrAdd(userId, _ => new ConcurrentQueue<DateTimeOffset>())
            .Enqueue(DateTimeOffset.UtcNow);
        var simulation = _simulations.GetValueOrDefault(userId, PaymentGatewaySimulation.Success);

        return simulation switch
        {
            PaymentGatewaySimulation.TransientThenSuccess when attempt == 1 =>
                PaymentOutcomeDecision.RetriableTechnicalFailure("temporary"),
            PaymentGatewaySimulation.AlwaysDegradedRecoverable =>
                PaymentOutcomeDecision.RetriableTechnicalFailure("provider degraded"),
            PaymentGatewaySimulation.Terminal =>
                PaymentOutcomeDecision.NonRetriableTechnicalFailure("terminal"),
            PaymentGatewaySimulation.BusinessDecline =>
                PaymentOutcomeDecision.BusinessDecline("declined"),
            _ => PaymentOutcomeDecision.Success()
        };
    }
}

public enum PaymentGatewaySimulation
{
    Success,
    TransientThenSuccess,
    AlwaysDegradedRecoverable,
    Terminal,
    BusinessDecline
}
