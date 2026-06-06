using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Bookings.Features.GetBookingDetails;
using ModularVerticalSlice.Application.Modules.Bookings.Features.GetCustomerBookings;
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

        var result = await handler.HandleCreateBooking(command, CancellationToken.None);

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

        var result = await handler.HandleCreateBooking(
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

        var result = await handler.HandleCreateBooking(
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
    /// Verifies that the Booking entity owns the natural lifecycle transitions.
    /// </summary>
    [Fact]
    public void Booking_Should_Apply_Natural_Lifecycle_Transitions()
    {
        var pendingForConfirm = new Booking { Status = BookingStatus.Pending };
        var pendingForCancel = new Booking { Status = BookingStatus.Pending };
        var pendingForExpire = new Booking { Status = BookingStatus.Pending };

        var confirmResult = pendingForConfirm.Confirm();
        var cancelResult = pendingForCancel.Cancel();
        var expireResult = pendingForExpire.Expire();

        Assert.True(confirmResult.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, pendingForConfirm.Status);

        Assert.True(cancelResult.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, pendingForCancel.Status);

        Assert.True(expireResult.IsSuccess);
        Assert.Equal(BookingStatus.Expired, pendingForExpire.Status);
    }

    /// <summary>
    /// Verifies that non-pending bookings cannot transition again through the entity behavior.
    /// </summary>
    [Fact]
    public void Booking_Should_Reject_Invalid_Lifecycle_Transitions()
    {
        var confirmed = new Booking { Status = BookingStatus.Confirmed };
        var cancelled = new Booking { Status = BookingStatus.Cancelled };
        var expired = new Booking { Status = BookingStatus.Expired };

        var confirmResult = confirmed.Confirm();
        var cancelResult = cancelled.Cancel();
        var expireResult = expired.Expire();

        Assert.True(confirmResult.IsFailure);
        Assert.Equal("Bookings.InvalidConfirmation", confirmResult.Error.Code);

        Assert.True(cancelResult.IsFailure);
        Assert.Equal("Bookings.InvalidCancellation", cancelResult.Error.Code);

        Assert.True(expireResult.IsFailure);
        Assert.Equal("Bookings.InvalidExpiration", expireResult.Error.Code);
    }

    /// <summary>
    /// Verifies that the confirm handler applies the entity transition to a pending booking.
    /// </summary>
    [Fact]
    public async Task ConfirmBookingCommand_Should_Update_Booking_Status_To_Confirmed()
    {
        var bookingId = Guid.NewGuid();
        await using var db = CreateDbContext();

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

        var handler = new BookingLifecycleHandler(db);

        var result = await handler.HandleConfirmBooking(new ConfirmBookingCommand(bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Confirmed, db.Bookings.Single(x => x.Id == bookingId).Status);
    }

    /// <summary>
    /// Verifies that the cancel handler applies the entity transition to a pending booking.
    /// </summary>
    [Fact]
    public async Task CancelBookingCommand_Should_Update_Booking_Status_To_Cancelled()
    {
        var bookingId = Guid.NewGuid();
        await using var db = CreateDbContext();

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

        var handler = new BookingLifecycleHandler(db);

        var result = await handler.HandleCancelBooking(new CancelBookingCommand(bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Cancelled, db.Bookings.Single(x => x.Id == bookingId).Status);
    }

    /// <summary>
    /// Verifies that the expire handler applies the entity transition to a pending booking.
    /// </summary>
    [Fact]
    public async Task ExpireBookingCommand_Should_Update_Booking_Status_To_Expired()
    {
        var bookingId = Guid.NewGuid();
        await using var db = CreateDbContext();

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

        var handler = new BookingLifecycleHandler(db);

        var result = await handler.HandleExpireBooking(new ExpireBookingCommand(bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BookingStatus.Expired, db.Bookings.Single(x => x.Id == bookingId).Status);
    }

    /// <summary>
    /// Verifies that lifecycle handlers return not found when the booking does not exist.
    /// </summary>
    [Fact]
    public async Task BookingLifecycleHandler_Should_Return_NotFound_For_Missing_Booking()
    {
        await using var db = CreateDbContext();
        var handler = new BookingLifecycleHandler(db);

        var result = await handler.HandleConfirmBooking(new ConfirmBookingCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Bookings.BookingNotFound", result.Error.Code);
    }

    /// <summary>
    /// Verifies that lifecycle handlers preserve the entity conflict when the transition is invalid.
    /// </summary>
    [Fact]
    public async Task BookingLifecycleHandler_Should_Return_Conflict_For_Invalid_Transition()
    {
        var bookingId = Guid.NewGuid();
        await using var db = CreateDbContext();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = Guid.NewGuid(),
            Quantity = 2,
            Status = BookingStatus.Cancelled,
            UserId = "user-1",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        var handler = new BookingLifecycleHandler(db);

        var result = await handler.HandleConfirmBooking(new ConfirmBookingCommand(bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Bookings.InvalidConfirmation", result.Error.Code);
        Assert.Equal(BookingStatus.Cancelled, db.Bookings.Single(x => x.Id == bookingId).Status);
    }

    /// <summary>
    /// Verifies that the current-user bookings query returns only bookings owned by the authenticated user.
    /// </summary>
    [Fact]
    public async Task GetCustomerBookings_Should_Return_Only_Current_User_Bookings()
    {
        await using var db = CreateDbContext();
        var currentUserId = "user-1";
        var firstEventId = Guid.NewGuid();
        var secondEventId = Guid.NewGuid();

        db.Events.AddRange(
            new Event
            {
                Id = firstEventId,
                Title = "OpenAI Build Day",
                Date = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
                TicketPrice = 25m,
                AvailableTickets = 10
            },
            new Event
            {
                Id = secondEventId,
                Title = "Agent Systems Live",
                Date = new DateTimeOffset(2026, 6, 2, 20, 0, 0, TimeSpan.Zero),
                TicketPrice = 40m,
                AvailableTickets = 10
            });

        db.Bookings.AddRange(
            new Booking
            {
                Id = Guid.NewGuid(),
                EventId = firstEventId,
                Quantity = 1,
                Status = BookingStatus.Pending,
                UserId = currentUserId,
                ClientRequestId = Guid.NewGuid(),
                CreatedAt = new DateTimeOffset(2026, 5, 23, 11, 0, 0, TimeSpan.Zero)
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                EventId = secondEventId,
                Quantity = 2,
                Status = BookingStatus.Confirmed,
                UserId = currentUserId,
                ClientRequestId = Guid.NewGuid(),
                CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
            },
            new Booking
            {
                Id = Guid.NewGuid(),
                EventId = secondEventId,
                Quantity = 3,
                Status = BookingStatus.Pending,
                UserId = "user-2",
                ClientRequestId = Guid.NewGuid(),
                CreatedAt = new DateTimeOffset(2026, 5, 23, 13, 0, 0, TimeSpan.Zero)
            });
        await db.SaveChangesAsync();

        var handler = new GetCustomerBookingsHandler(db, new FakeCurrentUserContext(currentUserId));

        var result = await handler.HandleGetCustomerBookings(new GetCustomerBookingsQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.All(result.Value, booking => Assert.Equal(currentUserId, db.Bookings.Single(x => x.Id == booking.Id).UserId));
        Assert.Equal(BookingStatus.Confirmed, result.Value[0].Status);
        Assert.Equal(BookingStatus.Pending, result.Value[1].Status);
        Assert.Equal("Agent Systems Live", result.Value[0].EventTitle);
        Assert.Equal(new DateTimeOffset(2026, 6, 2, 20, 0, 0, TimeSpan.Zero), result.Value[0].EventDate);
        Assert.Equal("OpenAI Build Day", result.Value[1].EventTitle);
    }

    /// <summary>
    /// Verifies that the pragmatic composite query reflects the current Catalog data from the shared store.
    /// </summary>
    [Fact]
    public async Task GetCustomerBookings_Should_Reflect_Current_Event_Data_From_Shared_Store()
    {
        await using var db = CreateDbContext();
        var currentUserId = "user-1";
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Initial title",
            Date = new DateTimeOffset(2026, 6, 1, 18, 0, 0, TimeSpan.Zero),
            TicketPrice = 25m,
            AvailableTickets = 10
        });

        db.Bookings.Add(new Booking
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            Quantity = 1,
            Status = BookingStatus.Pending,
            UserId = currentUserId,
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 11, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        var handler = new GetCustomerBookingsHandler(db, new FakeCurrentUserContext(currentUserId));

        var initial = await handler.HandleGetCustomerBookings(new GetCustomerBookingsQuery(), CancellationToken.None);
        Assert.True(initial.IsSuccess);
        Assert.Single(initial.Value);
        Assert.Equal("Initial title", initial.Value[0].EventTitle);

        var @event = await db.Events.SingleAsync(x => x.Id == eventId);
        @event.Title = "Renamed title";
        @event.Date = new DateTimeOffset(2026, 6, 4, 21, 0, 0, TimeSpan.Zero);
        await db.SaveChangesAsync();

        var updated = await handler.HandleGetCustomerBookings(new GetCustomerBookingsQuery(), CancellationToken.None);

        Assert.True(updated.IsSuccess);
        Assert.Single(updated.Value);
        Assert.Equal("Renamed title", updated.Value[0].EventTitle);
        Assert.Equal(new DateTimeOffset(2026, 6, 4, 21, 0, 0, TimeSpan.Zero), updated.Value[0].EventDate);
    }

    /// <summary>
    /// Verifies that the current-user bookings query requires an authenticated user.
    /// </summary>
    [Fact]
    public async Task GetCustomerBookings_Should_Return_Unauthorized_When_Current_User_Is_Missing()
    {
        await using var db = CreateDbContext();
        var handler = new GetCustomerBookingsHandler(db, new FakeCurrentUserContext(string.Empty));

        var result = await handler.HandleGetCustomerBookings(new GetCustomerBookingsQuery(), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Bookings.MissingCurrentUser", result.Error.Code);
    }

    /// <summary>
    /// Verifies that booking details are returned only for bookings owned by the current user.
    /// </summary>
    [Fact]
    public async Task GetBookingDetails_Should_Return_Current_User_Booking()
    {
        await using var db = CreateDbContext();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "OpenAI Conf",
            Date = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 49.90m,
            AvailableTickets = 10
        });

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

        var handler = new GetBookingDetailsHandler(db, new FakeCurrentUserContext("user-1"));

        var result = await handler.HandleGetBookingDetails(new GetBookingDetailsQuery(bookingId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(bookingId, result.Value.Id);
        Assert.Equal("OpenAI Conf", result.Value.EventTitle);
        Assert.Equal(new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero), result.Value.EventDate);
        Assert.Equal(eventId, result.Value.EventId);
        Assert.Equal(2, result.Value.Quantity);
        Assert.Equal(49.90m, result.Value.TicketPrice);
        Assert.Equal(99.80m, result.Value.TotalPrice);
        Assert.Equal("user-1", result.Value.UserId);
    }

    /// <summary>
    /// Verifies that booking details are not returned for another user's booking.
    /// </summary>
    [Fact]
    public async Task GetBookingDetails_Should_Return_NotFound_For_Different_User()
    {
        await using var db = CreateDbContext();
        var bookingId = Guid.NewGuid();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = Guid.NewGuid(),
            Quantity = 2,
            Status = BookingStatus.Pending,
            UserId = "user-2",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        var handler = new GetBookingDetailsHandler(db, new FakeCurrentUserContext("user-1"));

        var result = await handler.HandleGetBookingDetails(new GetBookingDetailsQuery(bookingId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Bookings.BookingNotFound", result.Error.Code);
    }

    /// <summary>
    /// Verifies that booking details require an authenticated current user.
    /// </summary>
    [Fact]
    public async Task GetBookingDetails_Should_Return_Unauthorized_When_Current_User_Is_Missing()
    {
        await using var db = CreateDbContext();
        var handler = new GetBookingDetailsHandler(db, new FakeCurrentUserContext(string.Empty));

        var result = await handler.HandleGetBookingDetails(new GetBookingDetailsQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Unauthorized, result.Error.Type);
        Assert.Equal("Bookings.MissingCurrentUser", result.Error.Code);
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

        await BookingLifecycleSagaHandler.HandleBookingCreated(message, CancellationToken.None);
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

        await BookingLifecycleSagaHandler.HandleBookingCreated(
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

        var result = await BookingLifecycleSagaHandler.HandlePaymentSucceeded(
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

        var result = await BookingLifecycleSagaHandler.HandlePaymentFailed(
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

        var result = await BookingLifecycleSagaHandler.HandlePaymentTimeoutExpired(
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

        var result = await BookingLifecycleSagaHandler.HandlePaymentSucceeded(
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
    /// Verifies that a late payment failure is ignored after the booking has already been confirmed.
    /// </summary>
    [Fact]
    public async Task PaymentFailedEvent_After_Confirmation_Should_Be_Ignored()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = eventId,
            Quantity = 2,
            Status = BookingStatus.Confirmed,
            UserId = "user-1",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        bus.WhenInvokedMessageOf<CancelBookingCommand>(x => x.BookingId == bookingId)
            .RespondWith(Result.Success());

        var result = await BookingLifecycleSagaHandler.HandlePaymentFailed(
            new PaymentFailedEvent(
                bookingId,
                Guid.NewGuid(),
                "Declined",
                new DateTimeOffset(2026, 5, 23, 12, 25, 0, TimeSpan.Zero)),
            eventId,
            2,
            db,
            bus,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(bus.Invoked, x =>
            x is Envelope { Message: CancelBookingCommand invoked } &&
            invoked.BookingId == bookingId);
        Assert.DoesNotContain(bus.Published, x =>
            x is Envelope { Message: ReleaseTicketsCommand published } &&
            published.BookingId == bookingId);
    }

    /// <summary>
    /// Verifies that a late timeout is ignored after the booking has already been confirmed.
    /// </summary>
    [Fact]
    public async Task BookingPaymentTimeoutExpiredEvent_After_Confirmation_Should_Be_Ignored()
    {
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await using var db = CreateDbContext();
        var bus = new TestMessageContext();

        db.Bookings.Add(new Booking
        {
            Id = bookingId,
            EventId = eventId,
            Quantity = 2,
            Status = BookingStatus.Confirmed,
            UserId = "user-1",
            ClientRequestId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)
        });
        await db.SaveChangesAsync();

        bus.WhenInvokedMessageOf<ExpireBookingCommand>(x => x.BookingId == bookingId)
            .RespondWith(Result.Success());

        var result = await BookingLifecycleSagaHandler.HandlePaymentTimeoutExpired(
            new BookingPaymentTimeoutExpiredEvent(
                bookingId,
                new DateTimeOffset(2026, 5, 23, 12, 30, 0, TimeSpan.Zero),
                eventId,
                2),
            db,
            bus,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(bus.Invoked, x =>
            x is Envelope { Message: ExpireBookingCommand invoked } &&
            invoked.BookingId == bookingId);
        Assert.DoesNotContain(bus.Published, x =>
            x is Envelope { Message: ReleaseTicketsCommand published } &&
            published.BookingId == bookingId);
    }

    /// <summary>
    /// Verifies that the Bookings module maps the baseline booking endpoints.
    /// </summary>
    [Fact]
    public void BookingsModule_Should_Map_Baseline_Booking_Endpoints()
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
        Assert.Contains("/api/v1/bookings/{id:guid}", routes);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static CreateBookingHandler CreateHandler(
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
