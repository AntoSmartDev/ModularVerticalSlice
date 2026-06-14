using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Delivery.BookingConfirmation;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Shared.Composition;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi;
using ModularVerticalSlice.WebApi.Infrastructure.Authentication;
using ModularVerticalSlice.WebApi.Infrastructure.Authorization;
using Wolverine;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

[Collection("Concurrent booking idempotency")]
public sealed class ConcurrentBookingIdempotencyHttpIntegrationTests
{
    [Fact]
    public async Task Concurrent_Duplicate_Http_Requests_Should_Return_The_Same_Booking_Once()
    {
        var eventId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();
        var interceptor = new SynchronizeIdempotencyPreChecksInterceptor(clientRequestId);
        await using var app = await StartApplicationAsync(interceptor);
        await SeedEventAsync(app, eventId);

        using var client = CreateClient(app);
        var command = new CreateBookingCommand(eventId, 2, clientRequestId);
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/v1/bookings/", command, TestContext.Current.CancellationToken),
            client.PostAsJsonAsync("/api/v1/bookings/", command, TestContext.Current.CancellationToken));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        var bookingIds = await Task.WhenAll(responses.Select(response =>
            response.Content.ReadFromJsonAsync<Guid>(TestContext.Current.CancellationToken)));
        Assert.Equal(bookingIds[0], bookingIds[1]);
        await WaitUntilAsync(() => ReadPaymentCountAsync(app, bookingIds[0]), 1, TimeSpan.FromSeconds(5));
        await AssertRemainsAsync(() => ReadPaymentCountAsync(app, bookingIds[0]), 1, TimeSpan.FromSeconds(1));

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var booking = Assert.Single(await db.Bookings
            .AsNoTracking()
            .Where(x => x.UserId == "idempotency-user" && x.ClientRequestId == clientRequestId)
            .ToListAsync(TestContext.Current.CancellationToken));

        Assert.Equal(booking.Id, bookingIds[0]);
        Assert.Equal(8, await db.Events
            .Where(x => x.Id == eventId)
            .Select(x => x.AvailableTickets)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<WebApplication> StartApplicationAsync(
        SynchronizeIdempotencyPreChecksInterceptor interceptor)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Environment.EnvironmentName = Environments.Development;
        builder.Configuration.AddJsonFile("appsettings.Development.json", optional: false);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Fake:UserId"] = "idempotency-user"
        });
        builder.WebHost.UseUrls("http://127.0.0.1:0");

        IApplicationBoundary[] boundaries =
        [
            new CatalogModule(),
            new BookingsModule(),
            new PaymentsModule(),
            new BookingConfirmationDeliveryModule()
        ];

        builder.Host.UseWolverine(options =>
            options.ConfigureApplicationMessaging(
                builder.Configuration,
                db => db.AddInterceptors(interceptor)));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        builder.Services.AddWebApiAuthentication(builder.Configuration, builder.Environment);
        builder.Services.AddWebApiAuthorization();
        builder.Services.AddApplicationBoundaries(builder.Configuration, boundaries);
        builder.Services.AddPersistence();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapApplicationBoundaries(boundaries);
        await app.StartAsync(TestContext.Current.CancellationToken);

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database
            .MigrateAsync(TestContext.Current.CancellationToken);

        return app;
    }

    private static HttpClient CreateClient(WebApplication app)
    {
        var addresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()?
            .Addresses;
        var address = Assert.Single(addresses!);
        return new HttpClient { BaseAddress = new Uri(address) };
    }

    private static async Task SeedEventAsync(WebApplication app, Guid eventId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Concurrent idempotency proof",
            Date = DateTimeOffset.UtcNow.AddDays(1),
            TicketPrice = 25m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<int> ReadPaymentCountAsync(WebApplication app, Guid bookingId)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Payments.CountAsync(
            x => x.BookingId == bookingId,
            TestContext.Current.CancellationToken);
    }

    private static async Task WaitUntilAsync(Func<Task<int>> read, int expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await read() == expected)
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        Assert.Equal(expected, await read());
    }

    private static async Task AssertRemainsAsync(Func<Task<int>> read, int expected, TimeSpan duration)
    {
        var deadline = DateTimeOffset.UtcNow + duration;
        while (DateTimeOffset.UtcNow < deadline)
        {
            Assert.Equal(expected, await read());
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }
    }

    private sealed class SynchronizeIdempotencyPreChecksInterceptor(Guid clientRequestId) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothPreChecksStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _matchingPreChecks;
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (IsIdempotencyPreCheck(command) && Interlocked.Increment(ref _matchingPreChecks) <= 2)
            {
                if (Volatile.Read(ref _matchingPreChecks) == 2)
                {
                    _bothPreChecksStarted.TrySetResult();
                }

                await _bothPreChecksStarted.Task.WaitAsync(cancellationToken);
            }

            return result;
        }

        private bool IsIdempotencyPreCheck(DbCommand command) =>
            command.CommandText.Contains("FROM bookings", StringComparison.OrdinalIgnoreCase) &&
            command.Parameters
                .Cast<DbParameter>()
                .Any(parameter => parameter.Value is Guid value && value == clientRequestId);
    }
}

[CollectionDefinition("Concurrent booking idempotency", DisableParallelization = true)]
public sealed class ConcurrentBookingIdempotencyCollection;
