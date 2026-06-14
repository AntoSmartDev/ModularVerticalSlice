using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularVerticalSlice.Application.Shared.Composition;

/// <summary>
/// Provides service registration helpers for application boundaries.
/// </summary>
public static class ApplicationBoundaryServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services exposed by each application boundary.
    /// </summary>
    /// <param name="services">The service collection used by the application host.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="boundaries">The boundaries to register.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationBoundaries(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<IApplicationBoundary> boundaries)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(boundaries);

        foreach (var boundary in boundaries)
        {
            boundary.RegisterServices(services, configuration);
        }

        return services;
    }
}
