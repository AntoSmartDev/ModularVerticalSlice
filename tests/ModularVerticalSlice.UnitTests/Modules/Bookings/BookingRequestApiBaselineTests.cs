using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingRequest;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.Persistence;
using BookingHandlerAlias = ModularVerticalSlice.Application.Modules.Bookings.Features.BookingRequest.BookingHandler;

namespace ModularVerticalSlice.UnitTests.Modules.Bookings;

/// <summary>
/// Verifies the baseline Bookings request API behavior and wiring.
/// </summary>
public class BookingRequestApiBaselineTests
{
    /// <summary>
    /// Verifies that a booking request creates a pending booking owned by the current user.
    /// </summary>
    [Fact]
    public async Task RequestBooking_Should_Create_Pending_Booking()
    {
        await using var db = CreateDbContext();
        var userContext = new FakeCurrentUserContext("user-1");
        var handler = CreateHandler(db, userContext);
        var command = new RequestBookingCommand(Guid.NewGuid(), 2, Guid.NewGuid());

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
    }

    /// <summary>
    /// Verifies that the same user and client request identifier return the existing booking.
    /// </summary>
    [Fact]
    public async Task RequestBooking_Should_Return_Existing_Booking_When_ClientRequestId_Is_Duplicated()
    {
        await using var db = CreateDbContext();
        var bookingId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var clientRequestId = Guid.NewGuid();

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
            new RequestBookingCommand(eventId, 3, clientRequestId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(bookingId, result.Value);
        Assert.Single(db.Bookings);
    }

    /// <summary>
    /// Verifies that the Bookings module maps the baseline booking request endpoint.
    /// </summary>
    [Fact]
    public void BookingsModule_Should_Map_Baseline_Booking_Request_Endpoint()
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

    private static BookingHandlerAlias CreateHandler(
        AppDbContext db,
        ICurrentUserContext currentUserContext,
        TimeProvider? timeProvider = null) =>
        new(
            db,
            currentUserContext,
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
