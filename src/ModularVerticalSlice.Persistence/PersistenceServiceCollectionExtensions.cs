using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularVerticalSlice.Persistence;

/// <summary>
/// Registers the concrete EF Core persistence services for the application.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=modularverticalslice;Username=postgres;Password=postgres";

    /// <summary>
    /// Adds the application persistence baseline backed by PostgreSQL.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database") ?? DefaultConnectionString;

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

        return services;
    }
}
