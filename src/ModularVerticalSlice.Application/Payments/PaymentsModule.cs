using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Modules.Shared.Modules;

namespace ModularVerticalSlice.Modules.Payments;

/// <summary>
/// Registers services and endpoints exposed by the Payments module.
/// </summary>
/// <remarks>
/// The module class is the entry point used by the WebApi composition root.
/// It must not contain business logic.
/// </remarks>
public sealed class PaymentsModule : IModule
{
    /// <inheritdoc />
    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
