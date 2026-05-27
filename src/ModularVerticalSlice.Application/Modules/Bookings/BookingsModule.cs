using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Shared.Modules;

namespace ModularVerticalSlice.Application.Modules.Bookings;

/// <summary>
/// Registers services and endpoints exposed by the Bookings module.
/// </summary>
/// <remarks>
/// The module class is the entry point used by the WebApi composition root.
/// It must not contain business logic.
/// </remarks>
public sealed class BookingsModule : IModule
{
    /// <inheritdoc />
    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateBookingHandler>();
        services.AddScoped<BookingLifecycleHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        CreateBookingEndpoint.Map(endpoints);
    }
}
