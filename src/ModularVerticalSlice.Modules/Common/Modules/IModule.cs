using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularVerticalSlice.Modules.Common.Modules;

/// <summary>
/// Defines the bootstrap contract used to compose a business module into the host.
/// </summary>
public interface IModule
{
    /// <summary>
    /// Registers module-specific services, options and infrastructure into the service collection.
    /// </summary>
    /// <param name="services">The service collection used by the application host.</param>
    /// <param name="configuration">The application configuration.</param>
    void RegisterModule(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Maps the HTTP endpoints exposed by the module.
    /// </summary>
    /// <param name="endpoints">The route builder used by the application host.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
