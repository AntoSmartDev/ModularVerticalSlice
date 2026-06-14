using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Shared.Modules;
using ModularVerticalSlice.Application.Delivery.BookingConfirmation;
using ModularVerticalSlice.Application.Modules.Payments;

namespace ModularVerticalSlice.UnitTests.Modules;

/// <summary>
/// Verifies the baseline application entry-point bootstrap classes.
/// </summary>
public class ModuleBootstrapTests
{
    /// <summary>
    /// Verifies that each baseline module implements <see cref="IModule" />.
    /// </summary>
    [Fact]
    public void Baseline_Modules_Should_Implement_IModule()
    {
        IModule[] modules =
        [
            new CatalogModule(),
            new BookingsModule(),
            new PaymentsModule(),
            new BookingConfirmationDeliveryModule()
        ];

        Assert.Equal(4, modules.Length);
    }

    /// <summary>
    /// Verifies that the baseline modules can be invoked without throwing.
    /// </summary>
    [Fact]
    public void Baseline_Modules_Should_Allow_Empty_Registration_And_Mapping()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        IModule[] modules =
        [
            new CatalogModule(),
            new BookingsModule(),
            new PaymentsModule(),
            new BookingConfirmationDeliveryModule()
        ];

        foreach (var module in modules)
        {
            module.RegisterModule(services, configuration);
            module.MapEndpoints(app);
        }
    }
}
