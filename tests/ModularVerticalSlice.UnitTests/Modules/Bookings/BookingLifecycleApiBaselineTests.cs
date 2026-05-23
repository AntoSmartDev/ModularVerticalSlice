using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.SharedKernel;
using Wolverine;

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
        var bus = CreateMessageBus(command, Result.Success());
        var handler = CreateHandler(db, bus, userContext);

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
        Assert.Equal(10, db.Events.Single(x => x.Id == eventId).AvailableTickets);
        Assert.Contains(bus.Invoked, x => x is Envelope { Message: ReserveTicketsCommand invoked } && invoked.EventId == eventId);
        Assert.Contains(bus.Published, x => x is Envelope { Message: BookingCreatedEvent published } && published.BookingId == booking.Id);
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

        var bus = CreateMessageBus(
            new CreateBookingCommand(eventId, 3, clientRequestId),
            Result.Success());
        var handler = CreateHandler(db, bus, new FakeCurrentUserContext("user-1"));

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

        var command = new CreateBookingCommand(eventId, 2, Guid.NewGuid());
        var bus = CreateMessageBus(
            command,
            Result.Failure(
                Error.Conflict(
                    "Catalog.NotEnoughTickets",
                    "Not enough tickets are available.")));
        var handler = CreateHandler(db, bus, new FakeCurrentUserContext("user-1"));

        var result = await handler.Handle(
            command,
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("Catalog.NotEnoughTickets", result.Error.Code);
        Assert.Empty(db.ChangeTracker.Entries<Booking>());
        Assert.Equal(1, db.Events.Single(x => x.Id == eventId).AvailableTickets);
        Assert.Empty(bus.Published);
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
    /// Verifies that the durable saga baseline publishes payment processing and schedules the timeout.
    /// </summary>
    [Fact]
    public async Task BookingLifecycleSagaHandler_Should_PublishPaymentCommand_And_ScheduleTimeout()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero);
        var bus = new TestMessageContext();
        var timeProvider = new FixedTimeProvider(createdAt);

        await BookingLifecycleSagaHandler.Handle(
            new BookingCreatedEvent(bookingId, eventId, "user-1", 2, createdAt),
            bus,
            timeProvider);

        Assert.Contains(bus.Published, x =>
            x is Envelope { Message: ProcessPaymentCommand published } &&
            published.BookingId == bookingId &&
            published.EventId == eventId &&
            published.UserId == "user-1" &&
            published.Quantity == 2);

        Assert.Contains(bus.ScheduledMessages(), x =>
            x.Message is BookingPaymentTimeoutExpiredEvent scheduled &&
            scheduled.BookingId == bookingId &&
            scheduled.ExpiredAt == createdAt.AddMinutes(15));
    }

    /// <summary>
    /// Verifies that a successful payment leads the saga to invoke booking confirmation.
    /// </summary>
    [Fact]
    public async Task PaymentSucceededEvent_Should_Invoke_ConfirmBookingCommand()
    {
        var bookingId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = Guid.NewGuid(),
            Quantity = 2,
            Status = BookingStatus.Pending,
            UserId = "user-1",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        bus.WhenInvokedMessageOf<ConfirmBookingCommand>(x => x.BookingId == bookingId)
            .RespondWith(Result.Success());

        var result = await BookingLifecycleSagaHandler.Handle(
            new PaymentSucceededEvent(
                bookingId,
                Guid.NewGuid(),
                new DateTimeOffset(2026, 5, 23, 12, 5, 0, TimeSpan.Zero)),
            db,
            bus,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(bus.Invoked, x =>
            x is Envelope { Message: ConfirmBookingCommand invoked } &&
            invoked.BookingId == bookingId);
    }

    /// <summary>
    /// Verifies that a failed payment leads the saga to cancel the booking and release tickets.
    /// </summary>
    [Fact]
    public async Task PaymentFailedEvent_Should_Invoke_CancelBooking_And_Publish_ReleaseTickets()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = eventId,
            Quantity = 3,
            Status = BookingStatus.Pending,
            UserId = "user-1",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        bus.WhenInvokedMessageOf<CancelBookingCommand>(x => x.BookingId == bookingId)
            .RespondWith(Result.Success());

        var result = await BookingLifecycleSagaHandler.Handle(
            new PaymentFailedEvent(
                bookingId,
                Guid.NewGuid(),
                "Declined",
                new DateTimeOffset(2026, 5, 23, 12, 10, 0, TimeSpan.Zero)),
            eventId,
            3,
            db,
            bus,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(bus.Invoked, x =>
            x is Envelope { Message: CancelBookingCommand invoked } &&
            invoked.BookingId == bookingId);
        Assert.Contains(bus.Published, x =>
            x is Envelope { Message: ReleaseTicketsCommand published } &&
            published.BookingId == bookingId &&
            published.EventId == eventId &&
            published.Quantity == 3);
    }

    /// <summary>
    /// Verifies that a payment-timeout expiration expires the booking and publishes ticket release.
    /// </summary>
    [Fact]
    public async Task BookingPaymentTimeoutExpiredEvent_Should_Invoke_ExpireBooking_And_Publish_ReleaseTickets()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var expiredAt = new DateTimeOffset(2026, 5, 23, 12, 15, 0, TimeSpan.Zero);
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = eventId,
            Quantity = 2,
            Status = BookingStatus.Pending,
            UserId = "user-1",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        bus.WhenInvokedMessageOf<ExpireBookingCommand>(x => x.BookingId == bookingId)
            .RespondWith(Result.Success());

        var result = await BookingLifecycleSagaHandler.Handle(
            new BookingPaymentTimeoutExpiredEvent(
                bookingId,
                expiredAt,
                eventId,
                2),
            db,
            bus,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(bus.Invoked, x =>
            x is Envelope { Message: ExpireBookingCommand invoked } &&
            invoked.BookingId == bookingId);
        Assert.Contains(bus.Published, x =>
            x is Envelope { Message: ReleaseTicketsCommand published } &&
            published.BookingId == bookingId &&
            published.EventId == eventId &&
            published.Quantity == 2);
    }

    /// <summary>
    /// Verifies the current baseline late-message behavior before saga state guards are introduced.
    /// </summary>
    [Fact]
    public async Task PaymentSucceededEvent_After_Timeout_Should_Be_Ignored()
    {
        var bookingId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = Guid.NewGuid(),
            Quantity = 2,
            Status = BookingStatus.Expired,
            UserId = "user-1",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        bus.WhenInvokedMessageOf<ConfirmBookingCommand>(x => x.BookingId == bookingId)
            .RespondWith(Result.Success());

        var result = await BookingLifecycleSagaHandler.Handle(
            new PaymentSucceededEvent(
                bookingId,
                Guid.NewGuid(),
                new DateTimeOffset(2026, 5, 23, 12, 20, 0, TimeSpan.Zero)),
            db,
            bus,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(bus.Invoked, x =>
            x is Envelope { Message: ConfirmBookingCommand invoked } &&
            invoked.BookingId == bookingId);
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

    private static BookingLifecycleHandler CreateHandler(
        AppDbContext db,
        TestMessageContext bus,
        ICurrentUserContext currentUserContext,
        TimeProvider? timeProvider = null) =>
        new(
            db,
            bus,
            currentUserContext,
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)));

    private static TestMessageContext CreateMessageBus(
        CreateBookingCommand command,
        Result reserveTicketsResult)
    {
        var bus = new TestMessageContext();
        bus.WhenInvokedMessageOf<ReserveTicketsCommand>(x =>
                x.EventId == command.EventId &&
                x.Quantity == command.Quantity)
            .RespondWith(reserveTicketsResult);

        return bus;
    }

    private sealed class FakeCurrentUserContext(string userId) : ICurrentUserContext
    {
        public string UserId { get; } = userId;
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
