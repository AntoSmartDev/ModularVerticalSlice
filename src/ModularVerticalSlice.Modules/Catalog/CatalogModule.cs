using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Modules.Catalog.Features.Events;
using ModularVerticalSlice.Modules.Shared.Modules;

namespace ModularVerticalSlice.Modules.Catalog;

/// <summary>
/// Registers services and endpoints exposed by the Catalog module.
/// </summary>
/// <remarks>
/// The module class is the entry point used by the WebApi composition root.
/// It must not contain business logic.
/// </remarks>
public sealed class CatalogModule : IModule
{
    /// <inheritdoc />
    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ModularVerticalSlice.Modules.Catalog.Features.Events.EventHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        EventEndpoints.Map(endpoints);
    }
}
