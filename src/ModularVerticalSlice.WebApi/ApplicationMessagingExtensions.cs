using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Modules.Payments.Persistence;
using ModularVerticalSlice.Persistence;
using Wolverine;
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

        options.UseRuntimeCompilation();
        options.Discovery.IncludeAssembly(typeof(BookingsModule).Assembly);
        ConfigureMiniDbContextServiceLocation(options);
        options.Policies.AutoApplyTransactions();
        options.AddSagaType<BookingLifecycleSaga>("booking_lifecycle_sagas");
        PaymentsRuntimeRecoveryPolicies.Configure(options);
        options
            .PersistMessagesWithPostgresql(connectionString, "messaging")
            .EnableMessageTransport(_ => { });
    }

    private static void ConfigureMiniDbContextServiceLocation(WolverineOptions options)
    {
        // Mini DbContexts intentionally share the scoped AppDbContext instance through
        // opaque factories. Wolverine 6 requires this narrow service-location allow-list.
        options.CodeGeneration.AlwaysUseServiceLocationFor<ICatalogReadDbContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<ICatalogWriteDbContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IBookingCatalogReadDbContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IBookingReadDbContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IBookingWriteDbContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IPaymentReadDbContext>();
        options.CodeGeneration.AlwaysUseServiceLocationFor<IPaymentWriteDbContext>();
    }
}
