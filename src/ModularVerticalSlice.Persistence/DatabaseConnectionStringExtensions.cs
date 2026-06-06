using Microsoft.Extensions.Configuration;

namespace ModularVerticalSlice.Persistence;

/// <summary>
/// Resolves the single database connection-string contract used by the application.
/// </summary>
public static class DatabaseConnectionStringExtensions
{
    /// <summary>
    /// Gets the configured database connection string or fails during startup.
    /// </summary>
    public static string GetRequiredDatabaseConnectionString(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException(
                "Connection string 'Database' is required. Configure ConnectionStrings:Database or the ConnectionStrings__Database environment variable.");
    }
}
