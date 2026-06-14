using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Delivery.BookingConfirmation;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Composition;
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
    /// Proves the correlated success continuation commits the booking confirmation
    /// through the Wolverine-owned EF transaction boundary.
    /// </summary>
    [Fact]
    public async Task BookingLifecycleSaga_Should_Commit_Successful_Booking_Transition_Through_Runtime_Transaction()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        using var host = await StartHostAsync();

        try
        {
            await SeedPendingBookingAsync(
                host,
                bookingId,
                eventId,
                createdAt,
                userId: "integration-user");

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

            var bookingStatus = await ReadBookingStatusAsync(host, bookingId);

            Assert.Equal(BookingStatus.Confirmed, bookingStatus);

            var lateTimeoutSession = await startSession.PlayScheduledMessagesAsync(
                TimeSpan.FromSeconds(10));

            Assert.Empty(lateTimeoutSession.AllExceptions());
            Assert.Null(await ReadSagaBodyAsync(host, bookingId));
        }
        finally
        {
            await DeleteSagaAsync(host, bookingId);
            await DeleteBookingAsync(host, bookingId);
            await DeleteEventAsync(host, eventId);
        }
    }

    /// <summary>
    /// Proves the correlated business-failure continuation commits the booking cancellation
    /// and ticket-release compensation.
    /// </summary>
    [Fact]
    public async Task BookingLifecycleSaga_Should_Commit_Cancellation_And_Ticket_Release_On_Business_Failure()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        using var host = await StartHostAsync();

        try
        {
            await SeedPendingBookingAsync(
                host,
                bookingId,
                eventId,
                createdAt,
                userId: "integration-user-declined");

            var startSession = await host.InvokeMessageAndWaitAsync(
                new BookingCreatedEvent(bookingId, eventId, "integration-user-declined", 2, createdAt));

            Assert.Empty(startSession.AllExceptions());
            Assert.NotNull(await ReadSagaBodyAsync(host, bookingId));

            await SeedEventAsync(
                host,
                eventId,
                createdAt,
                availableTickets: 8,
                ticketPrice: 35m);

            var continuationSession = await host.InvokeMessageAndWaitAsync(
                new PaymentFailedEvent(
                    bookingId,
                    Guid.NewGuid(),
                    "Payment was declined.",
                    createdAt.AddMinutes(1)));

            Assert.Empty(continuationSession.AllExceptions());
            Assert.Null(await ReadSagaBodyAsync(host, bookingId));

            var bookingStatus = await ReadBookingStatusAsync(host, bookingId);
            var availableTickets = await ReadAvailableTicketsAsync(host, eventId);

            Assert.Equal(BookingStatus.Cancelled, bookingStatus);
            Assert.Equal(10, availableTickets);
        }
        finally
        {
            await DeleteSagaAsync(host, bookingId);
            await DeleteBookingAsync(host, bookingId);
            await DeleteEventAsync(host, eventId);
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

        IApplicationBoundary[] boundaries =
        [
            new BookingsModule(),
            new PaymentsModule(),
            new BookingConfirmationDeliveryModule()
        ];

        builder.Services.AddApplicationBoundaries(builder.Configuration, boundaries);
        builder.Services.AddPersistence();
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
        DateTimeOffset createdAt,
        string userId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = eventId,
            Quantity = 2,
            Status = BookingStatus.Pending,
            UserId = userId,
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = createdAt
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedEventAsync(
        IHost host,
        Guid eventId,
        DateTimeOffset createdAt,
        int availableTickets,
        decimal ticketPrice)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Runtime integration event",
            Date = createdAt.AddDays(7),
            TicketPrice = ticketPrice,
            AvailableTickets = availableTickets
        });

        await db.SaveChangesAsync();
    }

    private static async Task<BookingStatus?> ReadBookingStatusAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Bookings
            .Where(booking => booking.Id == bookingId)
            .Select(booking => (BookingStatus?)booking.Status)
            .SingleOrDefaultAsync();
    }

    private static async Task<int?> ReadAvailableTicketsAsync(IHost host, Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        return await db.Events
            .Where(@event => @event.Id == eventId)
            .Select(@event => (int?)@event.AvailableTickets)
            .SingleOrDefaultAsync();
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

    private static async Task DeleteEventAsync(IHost host, Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Events
            .Where(@event => @event.Id == eventId)
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

/// <summary>
/// Verifies that DbContextSlice adapters wrap the same scoped AppDbContext instance.
/// If adapters held a separate instance, handler changes would not be committed by
/// Wolverine's transaction middleware, and bookings would remain in Pending state.
/// </summary>
public sealed class PersistenceRegistrationScopeTests
{
    [Fact]
    public void MiniDbContextAdapters_Wrap_Same_Scoped_AppDbContext_Instance()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.Development.json", optional: false);

        IApplicationBoundary[] boundaries = [new BookingsModule(), new PaymentsModule(), new BookingConfirmationDeliveryModule()];
        builder.Services.AddApplicationBoundaries(builder.Configuration, boundaries);
        builder.Services.AddPersistence();
        builder.UseWolverine(options => options.ConfigureApplicationMessaging(builder.Configuration));

        var host = builder.Build();

        using var scope = host.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var writeDb = scope.ServiceProvider.GetRequiredService<IBookingWriteDbContextSlice>();

        // EF Core caches DbSet<T> per context instance, so same DbSet reference = same AppDbContext instance.
        // If adapters wrapped a separate AppDbContext, writeDb.Bookings would be a different object.
        Assert.Same(appDb.Bookings, writeDb.Bookings);
    }
}

/// <summary>
/// Verifies that ConfirmBookingCommand commits the booking status without [Transactional],
/// relying solely on AutoApplyTransactions() and the adapter chain.
/// </summary>
public sealed class BookingLifecycleHandlerDirectCommitTests
{
    [Fact]
    public async Task ConfirmBookingCommand_Commits_Status_Without_Transactional_Attribute()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        using var host = await StartHostAsync();

        try
        {
            await SeedPendingBookingAsync(host, bookingId, eventId);

            var session = await host.InvokeMessageAndWaitAsync(
                new ConfirmBookingCommand(bookingId));

            Assert.Empty(session.AllExceptions());

            var status = await ReadBookingStatusAsync(host, bookingId);
            Assert.Equal(BookingStatus.Confirmed, status);
        }
        finally
        {
            await DeleteBookingAsync(host, bookingId);
        }
    }

    private static async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.Development.json", optional: false);

        IApplicationBoundary[] boundaries = [new BookingsModule(), new PaymentsModule(), new BookingConfirmationDeliveryModule()];
        builder.Services.AddApplicationBoundaries(builder.Configuration, boundaries);
        builder.Services.AddPersistence();
        builder.UseWolverine(options => options.ConfigureApplicationMessaging(builder.Configuration));

        var host = builder.Build();
        await host.StartAsync();

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        return host;
    }

    private static async Task SeedPendingBookingAsync(IHost host, Guid bookingId, Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = eventId,
            Quantity = 1,
            Status = BookingStatus.Pending,
            UserId = "direct-test-user",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<BookingStatus?> ReadBookingStatusAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings
            .Where(b => b.Id == bookingId)
            .Select(b => (BookingStatus?)b.Status)
            .SingleOrDefaultAsync();
    }

    private static async Task DeleteBookingAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Bookings.Where(b => b.Id == bookingId).ExecuteDeleteAsync();
    }
}
