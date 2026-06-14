using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Catalog;
using ModularVerticalSlice.Application.Delivery.BookingConfirmation;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Shared.Composition;

namespace ModularVerticalSlice.UnitTests.Shared.Composition;

/// <summary>
/// Verifies the baseline application-boundary bootstrap classes.
/// </summary>
public class ApplicationBoundaryBootstrapTests
{
    /// <summary>
    /// Verifies that each baseline boundary implements
    /// <see cref="IApplicationBoundary" />.
    /// </summary>
    [Fact]
    public void Baseline_Boundaries_Should_Implement_IApplicationBoundary()
    {
        IApplicationBoundary[] boundaries =
        [
            new CatalogModule(),
            new BookingsModule(),
            new PaymentsModule(),
            new BookingConfirmationDeliveryModule()
        ];

        Assert.Equal(4, boundaries.Length);
    }

    /// <summary>
    /// Verifies that the baseline boundaries can be invoked without throwing.
    /// </summary>
    [Fact]
    public void Baseline_Boundaries_Should_Allow_Empty_Registration_And_Mapping()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        IApplicationBoundary[] boundaries =
        [
            new CatalogModule(),
            new BookingsModule(),
            new PaymentsModule(),
            new BookingConfirmationDeliveryModule()
        ];

        foreach (var boundary in boundaries)
        {
            boundary.RegisterServices(services, configuration);
            boundary.MapEndpoints(app);
        }
    }
}
