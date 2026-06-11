using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Modules.Payments.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Notifications;
using ModularVerticalSlice.Persistence;
using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RDBMS;

namespace ModularVerticalSlice.WebApi;

/// <summary>
/// Holds the explicit Wolverine messaging configuration used by the host.
/// </summary>
public static class ApplicationMessagingExtensions
{
    /// <summary>
    /// Applies the shared Wolverine messaging baseline for the application host.
    /// </summary>
    /// <param name="options">The Wolverine options being configured.</param>
    /// <param name="configuration">The host configuration.</param>
    public static void ConfigureApplicationMessaging(
        this WolverineOptions options,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetRequiredDatabaseConnectionString();

        options.Services.AddDbContextWithWolverineIntegration<AppDbContext>(builder =>
            builder.UseNpgsql(connectionString));
        options.UseRuntimeCompilation();
        options.Discovery.IncludeAssembly(typeof(BookingsModule).Assembly);
        options.Policies.AutoApplyTransactions();
        options.AddSagaType<BookingLifecycleSaga>("booking_lifecycle_sagas");
        PaymentsRuntimeRecoveryPolicies.Configure(options);
        NotificationsRuntimeRecoveryPolicies.Configure(options);
        options.LocalQueueFor<BookingConfirmedEvent>().UseDurableInbox();
        options
            .PersistMessagesWithPostgresql(connectionString, "messaging")
            .EnableMessageTransport(_ => { });
    }
}
