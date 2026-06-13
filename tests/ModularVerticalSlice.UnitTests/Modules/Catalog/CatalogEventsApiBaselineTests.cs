using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Modules.Catalog.Features.CreateEvent;
using ModularVerticalSlice.Application.Modules.Catalog.Features.GetEventDetails;
using ModularVerticalSlice.Application.Modules.Catalog.Features.GetUpcomingEvents;
using ModularVerticalSlice.Application.Modules.Catalog.Features.ReleaseTickets;
using ModularVerticalSlice.Application.Modules.Catalog.Features.ReserveTickets;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.SharedKernel;
using Wolverine.Attributes;
using Wolverine.Persistence;
using CreateEventSliceHandler = ModularVerticalSlice.Application.Modules.Catalog.Features.CreateEvent.CreateEventHandler;
using GetEventDetailsSliceHandler = ModularVerticalSlice.Application.Modules.Catalog.Features.GetEventDetails.GetEventDetailsHandler;
using GetUpcomingEventsSliceHandler = ModularVerticalSlice.Application.Modules.Catalog.Features.GetUpcomingEvents.GetUpcomingEventsHandler;
using ReleaseTicketsSliceHandler = ModularVerticalSlice.Application.Modules.Catalog.Features.ReleaseTickets.ReleaseTicketsHandler;
using ReserveTicketsSliceHandler = ModularVerticalSlice.Application.Modules.Catalog.Features.ReserveTickets.ReserveTicketsHandler;

namespace ModularVerticalSlice.UnitTests.Modules.Catalog;

/// <summary>
/// Verifies the baseline Catalog events API behavior and wiring.
/// </summary>
public class CatalogEventsApiBaselineTests
{
    [Fact]
    public void Reservation_Concurrency_Retry_Should_Be_Scoped_To_ReserveTickets_Handler()
    {
        var reserveMethod = typeof(ReserveTicketsSliceHandler)
            .GetMethod(nameof(ReserveTicketsSliceHandler.HandleReserveTickets));
        var createMethod = typeof(CreateEventSliceHandler)
            .GetMethod(nameof(CreateEventSliceHandler.HandleCreateEvent));

        Assert.NotNull(reserveMethod);
        Assert.NotNull(createMethod);
        Assert.Single(reserveMethod.GetCustomAttributes(typeof(RetryNowAttribute), inherit: false));
        Assert.Empty(createMethod.GetCustomAttributes(typeof(RetryNowAttribute), inherit: false));
    }

    /// <summary>
    /// Verifies that event creation adds a new entity to the write-side DbSet.
    /// </summary>
    [Fact]
    public async Task CreateEvent_Should_Add_Event_To_Write_DbContext()
    {
        await using var db = CreateDbContext();
        var handler = CreateCreateEventHandler(db);

        var result = await handler.HandleCreateEvent(
            new CreateEventCommand("OpenAI Conf", new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero), 49.90m, 120),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Single(db.ChangeTracker.Entries<Event>());

        var created = db.ChangeTracker.Entries<Event>().Single().Entity;
        Assert.Equal("OpenAI Conf", created.Title);
        Assert.Equal(120, created.AvailableTickets);
        Assert.Equal(created.Id, result.Value.Id);
    }

    /// <summary>
    /// Verifies that only future events are returned by the upcoming-events query.
    /// </summary>
    [Fact]
    public async Task GetUpcomingEvents_Should_Return_Only_Future_Events()
    {
        await using var db = CreateDbContext();
        db.Events.AddRange(
            new Event
            {
                Id = Guid.NewGuid(),
                Title = "Past event",
                Date = new DateTimeOffset(2026, 5, 20, 8, 0, 0, TimeSpan.Zero),
                TicketPrice = 10,
                AvailableTickets = 10
            },
            new Event
            {
                Id = Guid.NewGuid(),
                Title = "Future event",
                Date = new DateTimeOffset(2026, 5, 25, 8, 0, 0, TimeSpan.Zero),
                TicketPrice = 15,
                AvailableTickets = 20
            });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = CreateGetUpcomingEventsHandler(db, new FixedTimeProvider(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.HandleGetUpcomingEvents(new GetUpcomingEventsQuery(), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var events = Assert.Single(result.Value);
        Assert.Equal("Future event", events.Title);
    }

    /// <summary>
    /// Verifies that querying a missing event returns a not-found result.
    /// </summary>
    [Fact]
    public async Task GetEventDetails_Should_Return_NotFound_When_Event_Does_Not_Exist()
    {
        await using var db = CreateDbContext();
        var handler = CreateGetEventDetailsHandler(db);

        var result = await handler.HandleGetEventDetails(new GetEventDetailsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Catalog.EventNotFound", result.Error.Code);
    }

    /// <summary>
    /// Verifies that reserving tickets decreases event availability.
    /// </summary>
    [Fact]
    public async Task ReserveTickets_Should_Decrease_AvailableTickets()
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Reserved event",
            Date = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 50,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = CreateReserveTicketsHandler(db);

        var (result, storageAction) = await handler.HandleReserveTickets(
            new ReserveTicketsCommand(eventId, 2, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.IsType<Update<Event>>(storageAction);
        Assert.Equal(8, db.Events.Single(x => x.Id == eventId).AvailableTickets);
    }

    /// <summary>
    /// Verifies that reserving more tickets than available fails with conflict.
    /// </summary>
    [Fact]
    public async Task ReserveTickets_Should_Fail_When_Not_Enough_Tickets()
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Limited event",
            Date = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 50,
            AvailableTickets = 1
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = CreateReserveTicketsHandler(db);

        var (result, storageAction) = await handler.HandleReserveTickets(
            new ReserveTicketsCommand(eventId, 2, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Catalog.NotEnoughTickets", result.Error.Code);
        Assert.IsType<Nothing<Event>>(storageAction);
        Assert.Equal(1, db.Events.Single(x => x.Id == eventId).AvailableTickets);
    }

    /// <summary>
    /// Verifies that releasing tickets increases event availability and signals an explicit update.
    /// </summary>
    [Fact]
    public async Task ReleaseTickets_Should_Increase_AvailableTickets()
    {
        await using var db = CreateDbContext();
        var eventId = Guid.NewGuid();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Compensated event",
            Date = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            TicketPrice = 50,
            AvailableTickets = 8
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var handler = CreateReleaseTicketsHandler(db);

        var (result, storageAction) = await handler.HandleReleaseTickets(
            new ReleaseTicketsCommand(eventId, 2, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.IsType<Update<Event>>(storageAction);
        Assert.Equal(10, db.Events.Single(x => x.Id == eventId).AvailableTickets);
    }

    /// <summary>
    /// Verifies that releasing tickets for a missing event returns not-found and no storage action.
    /// </summary>
    [Fact]
    public async Task ReleaseTickets_Should_Return_NotFound_When_Event_Does_Not_Exist()
    {
        await using var db = CreateDbContext();
        var handler = CreateReleaseTicketsHandler(db);

        var (result, storageAction) = await handler.HandleReleaseTickets(
            new ReleaseTicketsCommand(Guid.NewGuid(), 2, Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.NotFound, result.Error.Type);
        Assert.Equal("Catalog.EventNotFound", result.Error.Code);
        Assert.IsType<Nothing<Event>>(storageAction);
    }

    /// <summary>
    /// Verifies that the Catalog module maps the baseline event endpoints.
    /// </summary>
    [Fact]
    public void CatalogModule_Should_Map_Baseline_Event_Endpoints()
    {
        var builder = WebApplication.CreateBuilder();
        var module = new CatalogModule();

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

        Assert.Contains("/api/v1/events/", routes);
        Assert.Contains("/api/v1/events/", routes);
        Assert.Contains("/api/v1/events/{id:guid}", routes);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new AppDbContext(options);
    }

    private static CreateEventSliceHandler CreateCreateEventHandler(AppDbContext db) => new(db);

    private static GetUpcomingEventsSliceHandler CreateGetUpcomingEventsHandler(
        AppDbContext db,
        TimeProvider? timeProvider = null) =>
        new(
            db,
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)));

    private static GetEventDetailsSliceHandler CreateGetEventDetailsHandler(AppDbContext db) => new(db);

    private static ReserveTicketsSliceHandler CreateReserveTicketsHandler(AppDbContext db) => new(db);

    private static ReleaseTicketsSliceHandler CreateReleaseTicketsHandler(AppDbContext db) => new(db);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
