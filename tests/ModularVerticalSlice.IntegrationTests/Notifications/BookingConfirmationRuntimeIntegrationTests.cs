using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Notifications;
using ModularVerticalSlice.Application.Modules.Notifications.Features.BookingConfirmation;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Shared.Modules;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi;
using Wolverine;
using Wolverine.Runtime;
using Wolverine.Tracking;

namespace ModularVerticalSlice.IntegrationTests.Notifications;

/// <summary>
/// Proves notification idempotency and recovery through Wolverine and PostgreSQL.
/// </summary>
public sealed class BookingConfirmationRuntimeIntegrationTests
{
    [Fact]
    public async Task Duplicate_Envelope_Should_Send_Confirmation_Email_Once()
    {
        using var host = await StartHostAsync();
        var sender = host.Services.GetRequiredService<FakeBookingConfirmationEmailSender>();
        var message = NewMessage("duplicate-user");
        var envelopeId = Guid.NewGuid();

        await EnqueueAsync(host, message, envelopeId);
        await WaitUntilAsync(() => sender.AttemptsFor(message.UserId) == 1);
        await EnqueueAsync(host, message, envelopeId);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);

        Assert.Single(sender.Sent);
        Assert.Equal(1, sender.AttemptsFor(message.UserId));
    }

    [Fact]
    public async Task Transient_Failure_Should_Retry_And_Send_Once()
    {
        using var host = await StartHostAsync();
        var sender = host.Services.GetRequiredService<FakeBookingConfirmationEmailSender>();
        var message = NewMessage("transient-user");
        sender.Configure(message.UserId, EmailSenderSimulationMode.TransientFailure);

        var session = await host
            .TrackActivity()
            .SendMessageAndWaitAsync(message, new DeliveryOptions());

        Assert.Empty(session.AllExceptions());
        Assert.Single(sender.Sent);
        Assert.Equal(2, sender.AttemptsFor(message.UserId));
    }

    [Fact]
    public async Task Permanent_Failure_Should_Move_Message_To_Error_Queue()
    {
        using var host = await StartHostAsync();
        var sender = host.Services.GetRequiredService<FakeBookingConfirmationEmailSender>();
        var message = NewMessage("permanent-user");
        sender.Configure(message.UserId, EmailSenderSimulationMode.PermanentFailure);

        var session = await host
            .TrackActivity()
            .DoNotAssertOnExceptionsDetected()
            .SendMessageAndWaitAsync(message, new DeliveryOptions());

        Assert.NotEmpty(session.AllRecordsInOrder(MessageEventType.MovedToErrorQueue));
        Assert.Empty(sender.Sent);
        Assert.Equal(1, sender.AttemptsFor(message.UserId));
    }

    private static async Task<IHost> StartHostAsync()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddJsonFile("appsettings.Development.json", optional: false);

        IModule[] modules = [new BookingsModule(), new PaymentsModule(), new NotificationsModule()];
        builder.Services.AddApplicationModules(builder.Configuration, modules);
        builder.Services.AddPersistence();
        builder.UseWolverine(options => options.ConfigureApplicationMessaging(builder.Configuration));

        var host = builder.Build();
        await host.StartAsync();

        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();

        return host;
    }

    private static BookingConfirmedEvent NewMessage(string userId) =>
        new(Guid.NewGuid(), Guid.NewGuid(), userId, DateTimeOffset.UtcNow);

    private static async Task EnqueueAsync(IHost host, BookingConfirmedEvent message, Guid envelopeId)
    {
        var envelope = new Envelope(message, [])
        {
            Id = envelopeId,
            Destination = new Uri("local://booking-confirmed-event")
        };

        await host.Services
            .GetRequiredService<IWolverineRuntime>()
            .EnqueueDirectlyAsync([envelope]);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow >= timeoutAt)
            {
                throw new TimeoutException("The expected notification runtime condition was not reached.");
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
