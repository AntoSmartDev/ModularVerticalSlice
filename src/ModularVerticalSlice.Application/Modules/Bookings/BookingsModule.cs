using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CheckBookingPaymentEligibility;
using ModularVerticalSlice.Application.Modules.Bookings.Features.GetBookingDetails;
using ModularVerticalSlice.Application.Modules.Bookings.Features.GetCustomerBookings;
using ModularVerticalSlice.Application.Shared.Composition;

namespace ModularVerticalSlice.Application.Modules.Bookings;

/// <summary>
/// Registers services and endpoints exposed by the Bookings module.
/// </summary>
/// <remarks>
/// The boundary class is the entry point used by the WebApi composition root.
/// It must not contain business logic.
/// </remarks>
public sealed class BookingsModule : IApplicationBoundary
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BookingLifecycleOptions>(
            configuration.GetSection("Bookings:Lifecycle"));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CreateBookingHandler>();
        services.AddScoped<BookingLifecycleHandler>();
        services.AddScoped<CheckBookingPaymentEligibilityHandler>();
        services.AddScoped<GetCustomerBookingsHandler>();
        services.AddScoped<GetBookingDetailsHandler>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        CreateBookingEndpoint.Map(endpoints);
        GetCustomerBookingsEndpoint.Map(endpoints);
        GetBookingDetailsEndpoint.Map(endpoints);
    }
}
