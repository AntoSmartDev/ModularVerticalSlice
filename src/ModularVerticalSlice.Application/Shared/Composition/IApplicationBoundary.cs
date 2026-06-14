using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ModularVerticalSlice.Application.Shared.Composition;

/// <summary>
/// Defines the bootstrap contract used to compose an application boundary into
/// the host.
/// </summary>
public interface IApplicationBoundary
{
    /// <summary>
    /// Registers boundary-specific services, options and infrastructure into
    /// the service collection.
    /// </summary>
    /// <param name="services">The service collection used by the application host.</param>
    /// <param name="configuration">The application configuration.</param>
    void RegisterServices(IServiceCollection services, IConfiguration configuration);

    /// <summary>
    /// Maps the HTTP endpoints exposed by the boundary.
    /// </summary>
    /// <param name="endpoints">The route builder used by the application host.</param>
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
