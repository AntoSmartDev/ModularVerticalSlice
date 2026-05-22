using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Modules.Catalog;
using ModularVerticalSlice.Modules.Catalog.Features.Events;
using ModularVerticalSlice.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.SharedKernel;
using CatalogEventHandler = ModularVerticalSlice.Modules.Catalog.Features.Events.EventHandler;

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
