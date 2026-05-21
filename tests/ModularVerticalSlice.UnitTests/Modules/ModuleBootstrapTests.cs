using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Modules.Bookings;
using ModularVerticalSlice.Modules.Catalog;
using ModularVerticalSlice.Modules.Common.Modules;
using ModularVerticalSlice.Modules.Notifications;
using ModularVerticalSlice.Modules.Payments;

namespace ModularVerticalSlice.UnitTests.Modules;

/// <summary>
/// Verifies the baseline module bootstrap classes.
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
            new NotificationsModule()
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
            new NotificationsModule()
        ];

        foreach (var module in modules)
        {
            module.RegisterModule(services, configuration);
            module.MapEndpoints(app);
        }
    }
}
