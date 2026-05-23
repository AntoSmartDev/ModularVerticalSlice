using Wolverine;
using Wolverine.Postgresql;

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

        options.Policies.AutoApplyTransactions();
        options
            .PersistMessagesWithPostgresql(connectionString, "messaging")
            .EnableMessageTransport(_ => { });
    }
}
