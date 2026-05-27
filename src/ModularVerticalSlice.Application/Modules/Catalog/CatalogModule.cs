using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Catalog.Features.CreateEvent;
using ModularVerticalSlice.Application.Modules.Catalog.Features.GetEventDetails;
using ModularVerticalSlice.Application.Modules.Catalog.Features.GetUpcomingEvents;
using ModularVerticalSlice.Application.Modules.Catalog.Features.ReleaseTickets;
using ModularVerticalSlice.Application.Modules.Catalog.Features.ReserveTickets;
using ModularVerticalSlice.Application.Shared.Modules;

namespace ModularVerticalSlice.Application.Modules.Catalog;

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
        services.AddScoped<CreateEventHandler>();
        services.AddScoped<GetUpcomingEventsHandler>();
        services.AddScoped<GetEventDetailsHandler>();
        services.AddScoped<ReserveTicketsHandler>();
        services.AddScoped<ReleaseTicketsHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        CreateEventEndpoint.Map(endpoints);
        GetUpcomingEventsEndpoint.Map(endpoints);
        GetEventDetailsEndpoint.Map(endpoints);
    }
}
