using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Features.CreateEvent;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Persistence;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

/// <summary>
/// Proves that the command HTTP endpoints now flow through the Wolverine runtime
/// (<c>bus.InvokeAsync</c>) and durably commit. These end-to-end HTTP tests drive
/// the real ASP.NET endpoint -> bus -> handler path and read back from a fresh scope.
/// Before F04 the endpoints called handlers directly, so these inserts were never
/// committed; these tests guard against that regression.
/// </summary>
public sealed class CommandEndpointsHttpIntegrationTests
{
    /// <summary>
    /// Proves <c>POST /api/v1/events/</c> commits the event through the bus.
    /// </summary>
    [Fact]
    public async Task CreateEvent_Endpoint_Commits_Event_Through_Bus()
    {
        await using var factory = new DevWebApplicationFactory();
        var client = factory.CreateClient();
        await MigrateAsync(factory);

        var command = new CreateEventCommand(
            "HTTP E2E Event",
            DateTimeOffset.UtcNow.AddDays(30),
            19.90m,
            50);

        var response = await client.PostAsJsonAsync(
            "/api/v1/events/",
            command,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<EventReadModel>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(created);

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var persisted = await db.Events
                .AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == created!.Id, TestContext.Current.CancellationToken);

            Assert.NotNull(persisted);
            Assert.Equal("HTTP E2E Event", persisted!.Title);
            Assert.Equal(50, persisted.AvailableTickets);
        }
        finally
        {
            await DeleteEventAsync(factory, created!.Id);
        }
    }

    /// <summary>
    /// Proves <c>POST /api/v1/bookings/</c> commits the booking through the bus,
    /// in the same Wolverine-owned transaction as the cross-module ticket reservation.
    /// </summary>
    [Fact]
    public async Task CreateBooking_Endpoint_Commits_Booking_Through_Bus()
    {
        await using var factory = new DevWebApplicationFactory();
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
        Assert.NotEqual(Guid.Empty, bookingId);

        try
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // The booking row exists => the command committed through the bus.
            // Before F04 the HTTP path never persisted the booking.
            var persisted = await db.Bookings
                .AsNoTracking()
                .SingleOrDefaultAsync(b => b.Id == bookingId, TestContext.Current.CancellationToken);

            Assert.NotNull(persisted);
            Assert.Equal(eventId, persisted!.EventId);
        }
        finally
        {
            await DeleteBookingAsync(factory, bookingId);
            await DeleteSagaAsync(factory, bookingId);
            await DeleteEventAsync(factory, eventId);
        }
    }

    private sealed class DevWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            // Development loads appsettings.Development.json (connection string)
            // and explicitly enables FakeAuth used by the booking endpoint.
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
        }
    }

    private static async Task MigrateAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database
            .MigrateAsync();
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
            Title = "Booking endpoint seed event",
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
}
