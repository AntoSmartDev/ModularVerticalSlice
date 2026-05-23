using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Features.Events;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.Persistence;
using BookingLifecycleHandlerAlias = ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle.BookingLifecycleHandler;
using CatalogEventHandlerAlias = ModularVerticalSlice.Application.Modules.Catalog.Features.Events.EventHandler;

namespace ModularVerticalSlice.UnitTests.Modules.Bookings;

/// <summary>
/// Verifies the baseline Bookings lifecycle API behavior and wiring.
/// </summary>
public class BookingLifecycleApiBaselineTests
{
    /// <summary>
    /// Verifies that a create-booking request creates a pending booking owned by the current user.
    /// </summary>
    [Fact]
    public async Task CreateBooking_Should_Create_Pending_Booking()
    {
        await using var db = CreateDbContext();
        var userContext = new FakeCurrentUserContext("user-1");
        var handler = CreateHandler(db, userContext);
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "OpenAI Conf",
            Date = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 49.90m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync();

        var command = new CreateBookingCommand(eventId, 2, Guid.NewGuid());

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(db.ChangeTracker.Entries<Booking>());

        var booking = db.ChangeTracker.Entries<Booking>().Single().Entity;
        Assert.Equal(result.Value, booking.Id);
        Assert.Equal(command.EventId, booking.EventId);
        Assert.Equal(command.Quantity, booking.Quantity);
        Assert.Equal(command.ClientRequestId, booking.ClientRequestId);
        Assert.Equal("user-1", booking.UserId);
        Assert.Equal(BookingStatus.Pending, booking.Status);
        Assert.Equal(8, db.Events.Single(x => x.Id == eventId).AvailableTickets);
    }

    /// <summary>
    /// Verifies that the same user and client request identifier return the existing booking.
    /// </summary>
    [Fact]
    public async Task CreateBooking_Should_Return_Existing_Booking_When_ClientRequestId_Is_Duplicated()
    {
        await using var db = CreateDbContext();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Existing event",
            Date = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 25m,
            AvailableTickets = 5
        });

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = eventId,
            Quantity = 3,
            Status = BookingStatus.Pending,
            UserId = "user-1",
            ClientRequestId = clientRequestId,
            CreatedAt = new DateTimeOffset(2026, 5, 23, 10, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, new FakeCurrentUserContext("user-1"));

        var result = await handler.Handle(
            new CreateBookingCommand(eventId, 3, clientRequestId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(bookingId, result.Value);
        Assert.Single(db.Bookings);
        Assert.Equal(5, db.Events.Single(x => x.Id == eventId).AvailableTickets);
    }

    /// <summary>
    /// Verifies that a booking is not created when ticket reservation fails in Catalog.
    /// </summary>
    [Fact]
    public async Task CreateBooking_Should_Fail_When_ReserveTickets_Fails()
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Limited event",
            Date = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 20m,
            AvailableTickets = 1
        });
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, new FakeCurrentUserContext("user-1"));

        var result = await handler.Handle(
            new CreateBookingCommand(eventId, 2, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Catalog.NotEnoughTickets", result.Error.Code);
        Assert.Empty(db.ChangeTracker.Entries<Booking>());
        Assert.Equal(1, db.Events.Single(x => x.Id == eventId).AvailableTickets);
    }

    /// <summary>
    /// Verifies the shape of the lifecycle commands and the booking-created event.
    /// </summary>
    [Fact]
    public void BookingLifecycle_Contracts_Should_Expose_Stable_Shape()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);

        var confirm = new ConfirmBookingCommand(bookingId);
        var cancel = new CancelBookingCommand(bookingId);
        var expire = new ExpireBookingCommand(bookingId);
        var created = new BookingCreatedEvent(bookingId, eventId, "user-1", 2, createdAt);

        Assert.Equal(bookingId, confirm.BookingId);
        Assert.Equal(bookingId, cancel.BookingId);
        Assert.Equal(bookingId, expire.BookingId);

        Assert.Equal(bookingId, created.BookingId);
        Assert.Equal(eventId, created.EventId);
        Assert.Equal("user-1", created.UserId);
        Assert.Equal(2, created.Quantity);
        Assert.Equal(createdAt, created.CreatedAt);
    }

    /// <summary>
    /// Verifies the shape of the payment command and booking payment-timeout event.
    /// </summary>
    [Fact]
    public void BookingLifecycle_Saga_Contracts_Should_Expose_Stable_Shape()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var expiredAt = new DateTimeOffset(2026, 5, 23, 12, 30, 0, TimeSpan.Zero);

        var processPayment = new ProcessPaymentCommand(
            bookingId,
            eventId,
            "user-1",
            2);
        var timeoutExpired = new BookingPaymentTimeoutExpiredEvent(
            bookingId,
            expiredAt);

        Assert.Equal(bookingId, processPayment.BookingId);
        Assert.Equal(eventId, processPayment.EventId);
        Assert.Equal("user-1", processPayment.UserId);
        Assert.Equal(2, processPayment.Quantity);

        Assert.Equal(bookingId, timeoutExpired.BookingId);
        Assert.Equal(expiredAt, timeoutExpired.ExpiredAt);
    }

    /// <summary>
    /// Verifies that the saga baseline accepts the initial booking-created handoff.
    /// </summary>
    [Fact]
    public async Task BookingLifecycleSagaHandler_Should_Accept_BookingCreatedEvent()
    {
        var message = new BookingCreatedEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "user-1",
            2,
            new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero));

        await BookingLifecycleSagaHandler.Handle(message, CancellationToken.None);
    }

    /// <summary>
    /// Verifies that the Bookings module maps the baseline booking lifecycle endpoint.
    /// </summary>
    [Fact]
    public void BookingsModule_Should_Map_Baseline_Booking_Lifecycle_Endpoint()
    {
        var builder = WebApplication.CreateBuilder();
        var module = new BookingsModule();

        module.RegisterModule(builder.Services, builder.Configuration);

        var app = builder.Build();
        module.MapEndpoints(app);

        var endpoints = (IEndpointRouteBuilder)app;

        var routes = endpoints.DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Where(pattern => pattern is not null)
            .ToArray();

        Assert.Contains("/api/v1/bookings/", routes);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static BookingLifecycleHandlerAlias CreateHandler(
        AppDbContext db,
        ICurrentUserContext currentUserContext,
        TimeProvider? timeProvider = null) =>
        new(
            db,
            CreateCatalogHandler(db, timeProvider),
            currentUserContext,
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)));

    private static CatalogEventHandlerAlias CreateCatalogHandler(
        AppDbContext db,
        TimeProvider? timeProvider = null) =>
        new(
            db,
            db,
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)));

    private sealed class FakeCurrentUserContext(string userId) : ICurrentUserContext
    {
        public string UserId { get; } = userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
