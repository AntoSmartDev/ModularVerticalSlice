using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Shared.Modules;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi;
using Wolverine;
using Wolverine.Tracking;

namespace ModularVerticalSlice.IntegrationTests.Bookings;

/// <summary>
/// Verifies the BookingLifecycle saga through the concrete Wolverine and PostgreSQL runtime.
/// </summary>
public sealed class BookingLifecycleSagaRuntimeIntegrationTests
{
    /// <summary>
    /// Proves persistence, BookingId correlation, terminal completion, and late-message handling.
    /// </summary>
    [Fact]
    public async Task BookingLifecycleSaga_Should_Persist_Correlate_Complete_And_Ignore_Late_Message()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        using var host = await StartHostAsync();

        try
        {
            await SeedPendingBookingAsync(host, bookingId, eventId, createdAt);

            var startSession = await host.InvokeMessageAndWaitAsync(
                new BookingCreatedEvent(bookingId, eventId, "integration-user", 2, createdAt));

            Assert.Empty(startSession.AllExceptions());

            var persistedSaga = await ReadSagaBodyAsync(host, bookingId);
            Assert.NotNull(persistedSaga);
            Assert.Contains(bookingId.ToString(), persistedSaga, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(eventId.ToString(), persistedSaga, StringComparison.OrdinalIgnoreCase);

            var continuationSession = await host.InvokeMessageAndWaitAsync(
                new PaymentSucceededEvent(bookingId, Guid.NewGuid(), createdAt.AddMinutes(1)));

            Assert.Empty(continuationSession.AllExceptions());
            Assert.Null(await ReadSagaBodyAsync(host, bookingId));

            var lateTimeoutSession = await startSession.PlayScheduledMessagesAsync(
                TimeSpan.FromSeconds(10));

            Assert.Empty(lateTimeoutSession.AllExceptions());
            Assert.Null(await ReadSagaBodyAsync(host, bookingId));
        }
        finally
        {
            await DeleteSagaAsync(host, bookingId);
            await DeleteBookingAsync(host, bookingId);
        }
    }

    private static async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile(
            "appsettings.Development.json",
            optional: false);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Bookings:Lifecycle:PaymentWindow"] = "01:00:00"
        });

        IModule[] modules =
        [
            new BookingsModule(),
            new PaymentsModule()
        ];

        builder.Services.AddApplicationModules(builder.Configuration, modules);
        builder.Services.AddPersistence(builder.Configuration);
        builder.UseWolverine(options => options.ConfigureApplicationMessaging(builder.Configuration));

        var host = builder.Build();
        await host.StartAsync();

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database
            .MigrateAsync();

        return host;
    }

    private static async Task SeedPendingBookingAsync(
        IHost host,
        Guid bookingId,
        Guid eventId,
        DateTimeOffset createdAt)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = eventId,
            Quantity = 2,
            Status = BookingStatus.Pending,
            UserId = "integration-user",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = createdAt
        });

        await db.SaveChangesAsync();
    }

    private static async Task<string?> ReadSagaBodyAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = db.Database.GetDbConnection();

        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select body::text from messaging.booking_lifecycle_sagas where id = @id";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = bookingId;
        command.Parameters.Add(parameter);

        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task DeleteBookingAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Bookings
            .Where(booking => booking.Id == bookingId)
            .ExecuteDeleteAsync();
    }

    private static async Task DeleteSagaAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
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
