using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Shared.Modules;

namespace ModularVerticalSlice.Application.Delivery.BookingConfirmation;

/// <summary>
/// Registers services and endpoints exposed by the booking-confirmation
/// delivery boundary.
/// </summary>
/// <remarks>
/// The module class is the entry point used by the WebApi composition root.
/// It must not contain business logic.
/// </remarks>
public sealed class BookingConfirmationDeliveryModule : IModule
{
    /// <inheritdoc />
    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<FakeBookingConfirmationEmailSender>();
        services.AddSingleton<IBookingConfirmationEmailSender>(
            serviceProvider => serviceProvider.GetRequiredService<FakeBookingConfirmationEmailSender>());
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
