using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Notifications;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Shared.Modules;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.SharedKernel;
using ModularVerticalSlice.WebApi;
using Wolverine;

namespace ModularVerticalSlice.IntegrationTests.Catalog;

/// <summary>
/// Proves that <c>ReserveTicketsHandler</c>, which now returns a Wolverine EF storage side effect
/// (<c>Storage.Update</c> / <c>Storage.Nothing</c>), commits the availability change through the
/// Wolverine-owned EF transaction when the command is invoked through the bus. The assertions read
/// from a fresh scope, so they prove the value was durably committed rather than only tracked.
/// </summary>
public sealed class ReserveTicketsStorageOperationIntegrationTests
{
    /// <summary>
    /// Proves the success path commits the decremented availability through the runtime transaction.
    /// </summary>
    [Fact]
    public async Task ReserveTickets_StorageUpdate_Commits_Availability_Through_Runtime_Transaction()
    {
        var eventId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        using var host = await StartHostAsync();

        try
        {
            await SeedEventAsync(host, eventId, createdAt, availableTickets: 10);

            await using (var scope = host.Services.CreateAsyncScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

                var result = await bus.InvokeAsync<Result>(
                    new ReserveTicketsCommand(eventId, 3, Guid.NewGuid()),
                    TestContext.Current.CancellationToken);

                Assert.True(result.IsSuccess);
            }

            // Fresh scope: proves the Storage.Update side effect was committed, not just tracked.
            var availableTickets = await ReadAvailableTicketsAsync(host, eventId);
            Assert.Equal(7, availableTickets);
        }
        finally
        {
            await DeleteEventAsync(host, eventId);
        }
    }

    /// <summary>
    /// Proves a business failure returns <c>Storage.Nothing</c> and leaves persistence untouched.
    /// </summary>
    [Fact]
    public async Task ReserveTickets_StorageNothing_Leaves_Availability_Unchanged_On_Business_Failure()
    {
        var eventId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;

        using var host = await StartHostAsync();

        try
        {
            await SeedEventAsync(host, eventId, createdAt, availableTickets: 1);

            await using (var scope = host.Services.CreateAsyncScope())
            {
                var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

                var result = await bus.InvokeAsync<Result>(
                    new ReserveTicketsCommand(eventId, 5, Guid.NewGuid()),
                    TestContext.Current.CancellationToken);

                Assert.True(result.IsFailure);
                Assert.Equal("Catalog.NotEnoughTickets", result.Error.Code);
            }

            var availableTickets = await ReadAvailableTicketsAsync(host, eventId);
            Assert.Equal(1, availableTickets);
        }
        finally
        {
            await DeleteEventAsync(host, eventId);
        }
    }

    private static async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile(
            "appsettings.Development.json",
            optional: false);

        IModule[] modules =
        [
            new CatalogModule(),
            new BookingsModule(),
            new PaymentsModule(),
            new NotificationsModule()
        ];

        builder.Services.AddApplicationModules(builder.Configuration, modules);
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

    private static async Task SeedEventAsync(
        IHost host,
        Guid eventId,
        DateTimeOffset createdAt,
        int availableTickets)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Storage operation integration event",
            Date = createdAt.AddDays(7),
            TicketPrice = 25m,
            AvailableTickets = availableTickets
        });

        await db.SaveChangesAsync();
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

    private static async Task DeleteEventAsync(IHost host, Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Events
            .Where(@event => @event.Id == eventId)
            .ExecuteDeleteAsync();
    }
}
