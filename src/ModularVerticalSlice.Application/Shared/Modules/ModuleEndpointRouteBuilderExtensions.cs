using Microsoft.AspNetCore.Routing;

namespace ModularVerticalSlice.Application.Shared.Modules;

/// <summary>
/// Provides endpoint mapping helpers for application modules.
/// </summary>
public static class ModuleEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the endpoints exposed by each application module.
    /// </summary>
    /// <param name="endpoints">The route builder used by the application host.</param>
    /// <param name="modules">The modules to map.</param>
    /// <returns>The same route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapApplicationModules(
        this IEndpointRouteBuilder endpoints,
        IEnumerable<IModule> modules)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(modules);

        foreach (var module in modules)
        {
            module.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
