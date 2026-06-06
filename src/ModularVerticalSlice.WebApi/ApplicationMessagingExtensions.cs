using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Payments;
using Wolverine;
using Wolverine.Postgresql;
using Wolverine.RDBMS;

namespace ModularVerticalSlice.WebApi;

/// <summary>
/// Holds the explicit Wolverine messaging configuration used by the host.
/// </summary>
public static class ApplicationMessagingExtensions
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=modularverticalslice;Username=postgres;Password=postgres";

    /// <summary>
    /// Applies the shared Wolverine messaging baseline for the application host.
    /// </summary>
    /// <param name="options">The Wolverine options being configured.</param>
    /// <param name="configuration">The host configuration.</param>
    public static void ConfigureApplicationMessaging(
        this WolverineOptions options,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database") ?? DefaultConnectionString;

        options.Discovery.IncludeAssembly(typeof(BookingsModule).Assembly);
        options.Policies.AutoApplyTransactions();
        options.AddSagaType<BookingLifecycleSaga>("booking_lifecycle_sagas");
        PaymentsRuntimeRecoveryPolicies.Configure(options);
        options
            .PersistMessagesWithPostgresql(connectionString, "messaging")
            .EnableMessageTransport(_ => { });
    }
}
