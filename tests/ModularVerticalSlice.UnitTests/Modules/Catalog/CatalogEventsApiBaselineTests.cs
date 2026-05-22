using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Modules.Catalog.Features.Events;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.SharedKernel;
using CatalogEventHandler = ModularVerticalSlice.Application.Modules.Catalog.Features.Events.EventHandler;

namespace ModularVerticalSlice.UnitTests.Modules.Catalog;

/// <summary>
/// Verifies the baseline Catalog events API behavior and wiring.
/// </summary>
public class CatalogEventsApiBaselineTests
{
    /// <summary>
    /// Verifies that event creation adds a new entity to the write-side DbSet.
    /// </summary>
    [Fact]
    public async Task CreateEvent_Should_Add_Event_To_Write_DbContext()
    {
        await using var db = CreateDbContext();
        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new CreateEventCommand("OpenAI Conf", new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero), 49.90m, 120),
            CancellationToken.None);

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
        await db.SaveChangesAsync();

        var handler = CreateHandler(db, new FixedTimeProvider(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)));

        var result = await handler.Handle(new GetUpcomingEventsQuery(), CancellationToken.None);

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
        var handler = CreateHandler(db);

        var result = await handler.Handle(new GetEventDetailsQuery(Guid.NewGuid()), CancellationToken.None);

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
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new ReserveTicketsCommand(eventId, 2, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
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
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new ReserveTicketsCommand(eventId, 2, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("Catalog.NotEnoughTickets", result.Error.Code);
        Assert.Equal(1, db.Events.Single(x => x.Id == eventId).AvailableTickets);
    }

    /// <summary>
    /// Verifies that releasing tickets increases event availability.
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
        await db.SaveChangesAsync();

        var handler = CreateHandler(db);

        var result = await handler.Handle(
            new ReleaseTicketsCommand(eventId, 2, Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, db.Events.Single(x => x.Id == eventId).AvailableTickets);
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

    private static CatalogEventHandler CreateHandler(AppDbContext db, TimeProvider? timeProvider = null) =>
        new(
            db,
            db,
            timeProvider ?? new FixedTimeProvider(new DateTimeOffset(2026, 5, 23, 12, 0, 0, TimeSpan.Zero)));

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
