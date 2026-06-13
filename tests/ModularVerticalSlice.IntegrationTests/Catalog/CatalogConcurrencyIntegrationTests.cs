using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Notifications;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Shared.Modules;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.SharedKernel;
using ModularVerticalSlice.WebApi;
using Wolverine;

namespace ModularVerticalSlice.IntegrationTests.Catalog;

[Collection("Catalog concurrency")]
public sealed class CatalogConcurrencyIntegrationTests
{
    [Fact]
    public async Task Concurrent_Bookings_Should_Not_Overbook_The_Same_Event()
    {
        var eventId = Guid.NewGuid();
        var interceptor = new SynchronizeEventReadsInterceptor(eventId);
        using var host = await StartHostAsync(interceptor);
        await SeedEventAsync(host, eventId);

        var first = InvokeCreateBookingAsync(host, new CreateBookingCommand(eventId, 1, Guid.NewGuid()));
        var second = InvokeCreateBookingAsync(host, new CreateBookingCommand(eventId, 1, Guid.NewGuid()));
        var results = await Task.WhenAll(first, second);

        var success = Assert.Single(results, x => x.IsSuccess);
        var failure = Assert.Single(results, x => x.IsFailure);

        Assert.NotEqual(Guid.Empty, success.Value);
        Assert.Equal("Catalog.NotEnoughTickets", failure.Error.Code);
        Assert.Equal(0, await ReadAvailableTicketsAsync(host, eventId));

        var bookings = await ReadBookingsAsync(host, eventId);
        var booking = Assert.Single(bookings);
        Assert.Equal(success.Value, booking.Id);
        Assert.Equal(1, booking.Quantity);
    }

    private static async Task<IHost> StartHostAsync(SynchronizeEventReadsInterceptor interceptor)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.Development.json", optional: false);

        IModule[] modules =
        [
            new CatalogModule(),
            new BookingsModule(),
            new PaymentsModule(),
            new NotificationsModule()
        ];

        builder.Services.AddApplicationModules(builder.Configuration, modules);
        builder.Services.AddPersistence();
        builder.Services.AddSingleton<ICurrentUserContext>(new TestCurrentUserContext("concurrent-user"));
        builder.UseWolverine(options =>
            options.ConfigureApplicationMessaging(
                builder.Configuration,
                db => db.AddInterceptors(interceptor)));

        var host = builder.Build();
        await host.StartAsync();

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        return host;
    }

    private static async Task<Result<Guid>> InvokeCreateBookingAsync(
        IHost host,
        CreateBookingCommand command)
    {
        await using var scope = host.Services.CreateAsyncScope();
        return await scope.ServiceProvider
            .GetRequiredService<IMessageBus>()
            .InvokeAsync<Result<Guid>>(command, TestContext.Current.CancellationToken);
    }

    private static async Task SeedEventAsync(IHost host, Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Events.Add(new Event
        {
            Id = eventId,
            Title = "Catalog concurrency proof",
            Date = DateTimeOffset.UtcNow.AddDays(1),
            TicketPrice = 25m,
            AvailableTickets = 1
        });
        await db.SaveChangesAsync();
    }

    private static async Task<int> ReadAvailableTicketsAsync(IHost host, Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Events
            .Where(x => x.Id == eventId)
            .Select(x => x.AvailableTickets)
            .SingleAsync();
    }

    private static async Task<IReadOnlyList<Booking>> ReadBookingsAsync(IHost host, Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings
            .AsNoTracking()
            .Where(x => x.EventId == eventId)
            .ToListAsync();
    }

    private sealed class TestCurrentUserContext(string userId) : ICurrentUserContext
    {
        public string UserId { get; } = userId;
    }

    private sealed class SynchronizeEventReadsInterceptor(Guid eventId) : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothReadsStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _matchingReads;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (IsTargetEventRead(command))
            {
                if (Interlocked.Increment(ref _matchingReads) == 2)
                {
                    _bothReadsStarted.TrySetResult();
                }

                await _bothReadsStarted.Task.WaitAsync(cancellationToken);
            }

            return result;
        }

        private bool IsTargetEventRead(DbCommand command) =>
            command.CommandText.Contains("FROM events", StringComparison.OrdinalIgnoreCase) &&
            command.Parameters
                .Cast<DbParameter>()
                .Any(parameter => parameter.Value is Guid value && value == eventId);
    }
}

[CollectionDefinition("Catalog concurrency", DisableParallelization = true)]
public sealed class CatalogConcurrencyCollection;
