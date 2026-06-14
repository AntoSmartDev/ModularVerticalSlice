using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Catalog.Features.CreateEvent;
using ModularVerticalSlice.Application.Modules.Catalog.Features.GetEventDetails;
using ModularVerticalSlice.Application.Modules.Catalog.Features.GetEventTicketPrice;
using ModularVerticalSlice.Application.Modules.Catalog.Features.GetUpcomingEvents;
using ModularVerticalSlice.Application.Modules.Catalog.Features.ReleaseTickets;
using ModularVerticalSlice.Application.Modules.Catalog.Features.TicketReservation;
using ModularVerticalSlice.Application.Modules.Catalog.Contracts;
using ModularVerticalSlice.Application.Shared.Composition;

namespace ModularVerticalSlice.Application.Modules.Catalog;

/// <summary>
/// Registers services and endpoints exposed by the Catalog module.
/// </summary>
/// <remarks>
/// The boundary class is the entry point used by the WebApi composition root.
/// It must not contain business logic.
/// </remarks>
public sealed class CatalogModule : IApplicationBoundary
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateEventHandler>();
        services.AddScoped<GetUpcomingEventsHandler>();
        services.AddScoped<GetEventDetailsHandler>();
        services.AddScoped<GetEventTicketPriceHandler>();
        services.AddScoped<ITicketReservation, TicketReservation>();
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
