using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularVerticalSlice.Application.Shared.Observability;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi.Infrastructure.Observability;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

/// <summary>
/// Proves the correlation id reaches the logging scope on both the HTTP path and
/// the message-handler path, and that the small structured workflow events are
/// emitted with a correlation id that stays consistent across the message chain.
/// </summary>
[Collection("Correlation logging")]
public sealed class CorrelationLoggingIntegrationTests
{
    [Fact]
    public async Task BookingFlow_Emits_Structured_Events_With_Consistent_Message_CorrelationId()
    {
        var capture = new CapturingLoggerProvider();
        await using var factory = new CapturingWebApplicationFactory(capture);
        var client = factory.CreateClient();
        await MigrateAsync(factory);

        var eventId = Guid.NewGuid();
        await SeedEventAsync(factory, eventId, availableTickets: 5);

        var command = new CreateBookingCommand(eventId, 2, Guid.NewGuid());

        var response = await client.PostAsJsonAsync(
            "/api/v1/bookings/",
            command,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bookingId = await response.Content.ReadFromJsonAsync<Guid>(
            TestContext.Current.CancellationToken);

        try
        {
            // The happy path succeeds (random user id), so the booking is confirmed
            // through saga -> payment -> lifecycle, emitting both structured events.
            await WaitUntilAsync(
                () => PaymentLogFor(capture, bookingId) is not null
                    && BookingTransitionLogFor(capture, bookingId) is not null,
                TimeSpan.FromSeconds(15));

            var paymentLog = PaymentLogFor(capture, bookingId);
            var transitionLog = BookingTransitionLogFor(capture, bookingId);

            Assert.NotNull(paymentLog);
            Assert.NotNull(transitionLog);

            // Correlation id is present in the message-handler logging scope...
            Assert.False(string.IsNullOrEmpty(paymentLog!.CorrelationId));
            Assert.False(string.IsNullOrEmpty(transitionLog!.CorrelationId));

            // ...and is the same across the whole message chain.
            Assert.Equal(paymentLog.CorrelationId, transitionLog.CorrelationId);
        }
        finally
        {
            await DeleteBookingAsync(factory, bookingId);
            await DeleteSagaAsync(factory, bookingId);
            await DeleteEventAsync(factory, eventId);
        }
    }

    [Fact]
    public async Task HttpRequest_Puts_Supplied_CorrelationId_Into_The_Logging_Scope()
    {
        const string correlationId = "http-scope-correlation-id";
        var capture = new CapturingLoggerProvider();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(capture);
        builder.Logging.SetMinimumLevel(LogLevel.Information);
        builder.Services.AddCorrelation();

        var app = builder.Build();
        app.UseCorrelationId();
        app.MapGet("/log", (ILoggerFactory loggerFactory) =>
        {
            loggerFactory.CreateLogger("Endpoint").LogInformation("handled");
            return Results.Ok();
        });

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/log");
            request.Headers.Add("X-Correlation-Id", correlationId);

            var response = await app.GetTestClient()
                .SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var handled = capture.Entries.FirstOrDefault(e => e.Message == "handled");
            Assert.NotNull(handled);
            Assert.Equal(correlationId, handled!.CorrelationId);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static CapturedLog? PaymentLogFor(CapturingLoggerProvider capture, Guid bookingId) =>
        capture.Entries.FirstOrDefault(e =>
            e.Message.Contains("Payment", StringComparison.Ordinal)
            && e.Message.Contains("recorded", StringComparison.Ordinal)
            && e.Message.Contains(bookingId.ToString(), StringComparison.Ordinal));

    private static CapturedLog? BookingTransitionLogFor(CapturingLoggerProvider capture, Guid bookingId) =>
        capture.Entries.FirstOrDefault(e =>
            e.Message.Contains("transitioned", StringComparison.Ordinal)
            && e.Message.Contains(bookingId.ToString(), StringComparison.Ordinal));

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (condition())
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        Assert.True(condition(), $"Expected structured events were not captured within {timeout}.");
    }

    private static async Task MigrateAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    private static async Task SeedEventAsync(
        WebApplicationFactory<Program> factory,
        Guid eventId,
        int availableTickets)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Correlation logging seed event",
            Date = DateTimeOffset.UtcNow.AddDays(14),
            TicketPrice = 30m,
            AvailableTickets = availableTickets
        });
        await db.SaveChangesAsync();
    }

    private static async Task DeleteEventAsync(WebApplicationFactory<Program> factory, Guid eventId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Events.Where(e => e.Id == eventId).ExecuteDeleteAsync();
    }

    private static async Task DeleteBookingAsync(WebApplicationFactory<Program> factory, Guid bookingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Bookings.Where(b => b.Id == bookingId).ExecuteDeleteAsync();
    }

    private static async Task DeleteSagaAsync(WebApplicationFactory<Program> factory, Guid bookingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "delete from messaging.booking_lifecycle_sagas where id = @id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = bookingId;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync();
    }

    private sealed class CapturingWebApplicationFactory(CapturingLoggerProvider capture)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddProvider(capture);
                logging.SetMinimumLevel(LogLevel.Information);
            });
        }
    }

    private sealed record CapturedLog(string Category, string Message, string? CorrelationId);

    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public ConcurrentQueue<CapturedLog> Entries { get; } = new();

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

        public void Dispose() { }

        private string? ReadCorrelationId()
        {
            string? correlationId = null;
            _scopes.ForEachScope((scope, _) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object>> values)
                {
                    foreach (var kv in values)
                    {
                        if (kv.Key == CorrelationLoggingMiddleware.CorrelationIdKey)
                        {
                            correlationId = kv.Value?.ToString();
                        }
                    }
                }
            }, (object?)null);
            return correlationId;
        }

        private sealed class CapturingLogger(string category, CapturingLoggerProvider provider) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull
                => provider._scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                provider.Entries.Enqueue(new CapturedLog(
                    category,
                    formatter(state, exception),
                    provider.ReadCorrelationId()));
            }
        }
    }
}

[CollectionDefinition("Correlation logging", DisableParallelization = true)]
public sealed class CorrelationLoggingCollection;
