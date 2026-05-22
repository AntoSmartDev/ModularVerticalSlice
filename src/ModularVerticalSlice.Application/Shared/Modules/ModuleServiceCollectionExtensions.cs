using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularVerticalSlice.Application.Shared.Modules;

/// <summary>
/// Provides service registration helpers for application modules.
/// </summary>
public static class ModuleServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services exposed by each application module.
    /// </summary>
    /// <param name="services">The service collection used by the application host.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="modules">The modules to register.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddApplicationModules(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            module.RegisterModule(services, configuration);
        }

        return services;
    }
}
