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

namespace ModularVerticalSlice.IntegrationTests.Bookings;

[Collection("Create booking transactional outbox")]
public sealed class CreateBookingTransactionalOutboxIntegrationTests
{
    [Fact]
    public async Task Successful_CreateBooking_Should_Commit_Reservation_And_Deliver_Lifecycle_Once()
    {
        var eventId = Guid.NewGuid();
        using var host = await StartHostAsync();
        await SeedEventAsync(host, eventId);

        var result = await InvokeCreateBookingAsync(
            host,
            new CreateBookingCommand(eventId, 2, Guid.NewGuid()));

        Assert.True(result.IsSuccess);
        await WaitUntilAsync(() => ReadPaymentCountAsync(host, result.Value), 1, TimeSpan.FromSeconds(5));
        await AssertRemainsAsync(() => ReadPaymentCountAsync(host, result.Value), 1, TimeSpan.FromSeconds(1));

        var state = await ReadStateAsync(host, eventId);
        Assert.Equal(8, state.AvailableTickets);
        Assert.Equal(1, state.BookingCount);
        Assert.Equal(1, state.PaymentCount);
    }

    [Fact]
    public async Task Failed_Outer_Transaction_Should_Roll_Back_Reservation_And_Not_Deliver_Lifecycle()
    {
        var eventId = Guid.NewGuid();
        var interceptor = new FailCreateBookingSaveChangesInterceptor();
        using var host = await StartHostAsync(interceptor);
        await SeedEventAsync(host, eventId);
        interceptor.Arm();

        await Assert.ThrowsAnyAsync<Exception>(() => InvokeCreateBookingAsync(
            host,
            new CreateBookingCommand(eventId, 2, Guid.NewGuid())));

        Assert.NotEqual(Guid.Empty, interceptor.BookingId);
        await AssertRemainsAsync(
            () => ReadPaymentCountAsync(host, interceptor.BookingId),
            0,
            TimeSpan.FromSeconds(1));
        var state = await ReadStateAsync(host, eventId);

        Assert.True(interceptor.PublicationIntentWasPresent);
        Assert.Equal(10, state.AvailableTickets);
        Assert.Equal(0, state.BookingCount);
        Assert.Equal(0, state.PaymentCount);
    }

    private static async Task<IHost> StartHostAsync(SaveChangesInterceptor? interceptor = null)
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
        builder.Services.AddSingleton<ICurrentUserContext>(new TestCurrentUserContext("outbox-user"));
        builder.UseWolverine(options =>
            options.ConfigureApplicationMessaging(
                builder.Configuration,
                db =>
                {
                    if (interceptor is not null)
                    {
                        db.AddInterceptors(interceptor);
                    }
                }));

        var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database
            .MigrateAsync(TestContext.Current.CancellationToken);

        return host;
    }

    private static async Task<Result<Guid>> InvokeCreateBookingAsync(IHost host, CreateBookingCommand command)
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
            Title = "Transactional outbox proof",
            Date = DateTimeOffset.UtcNow.AddDays(1),
            TicketPrice = 25m,
            AvailableTickets = 10
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<(int AvailableTickets, int BookingCount, int PaymentCount)> ReadStateAsync(
        IHost host,
        Guid eventId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var bookingIds = db.Bookings
            .Where(x => x.EventId == eventId)
            .Select(x => x.Id);
        return (
            await db.Events.Where(x => x.Id == eventId).Select(x => x.AvailableTickets).SingleAsync(),
            await db.Bookings.CountAsync(x => x.EventId == eventId),
            await db.Payments.CountAsync(x => bookingIds.Contains(x.BookingId)));
    }

    private static async Task<int> ReadPaymentCountAsync(IHost host, Guid bookingId)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Payments.CountAsync(x => x.BookingId == bookingId);
    }

    private static async Task WaitUntilAsync(Func<Task<int>> read, int expected, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await read() == expected)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }

        Assert.Equal(expected, await read());
    }

    private static async Task AssertRemainsAsync(Func<Task<int>> read, int expected, TimeSpan duration)
    {
        var deadline = DateTimeOffset.UtcNow + duration;
        while (DateTimeOffset.UtcNow < deadline)
        {
            Assert.Equal(expected, await read());
            await Task.Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken);
        }
    }

    private sealed class FailCreateBookingSaveChangesInterceptor : SaveChangesInterceptor
    {
        private int _armed;
        public bool PublicationIntentWasPresent { get; private set; }
        public Guid BookingId { get; private set; }

        public void Arm() => Interlocked.Exchange(ref _armed, 1);

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref _armed) == 1 && eventData.Context is { } db)
            {
                var hasBooking = db.ChangeTracker.Entries<Booking>()
                    .Any(x => x.State == EntityState.Added);
                var hasReservation = db.ChangeTracker.Entries<Event>()
                    .Any(x => x.State == EntityState.Modified);

                if (hasBooking && hasReservation)
                {
                    BookingId = db.ChangeTracker.Entries<Booking>()
                        .Single(x => x.State == EntityState.Added)
                        .Entity
                        .Id;
                    PublicationIntentWasPresent = true;
                    throw new InvalidOperationException("Integration-only outer transaction failure.");
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class TestCurrentUserContext(string userId) : ICurrentUserContext
    {
        public string UserId { get; } = userId;
    }
}

[CollectionDefinition("Create booking transactional outbox", DisableParallelization = true)]
public sealed class CreateBookingTransactionalOutboxCollection;
