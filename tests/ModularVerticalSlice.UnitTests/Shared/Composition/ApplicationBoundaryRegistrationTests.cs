using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Shared.Composition;

namespace ModularVerticalSlice.UnitTests.Shared.Composition;

/// <summary>
/// Verifies the baseline behavior of application-boundary registration and
/// endpoint mapping extensions.
/// </summary>
public class ApplicationBoundaryRegistrationTests
{
    /// <summary>
    /// Verifies that service registration delegates to every supplied boundary.
    /// </summary>
    [Fact]
    public void AddApplicationBoundaries_Should_Register_All_Boundaries()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var boundaries = new IApplicationBoundary[]
        {
            new FakeBoundary(),
            new FakeBoundary()
        };

        services.AddApplicationBoundaries(configuration, boundaries);

        Assert.All(boundaries.Cast<FakeBoundary>(), boundary => Assert.Equal(1, boundary.RegisterCalls));
    }

    /// <summary>
    /// Verifies that endpoint mapping delegates to every supplied boundary.
    /// </summary>
    [Fact]
    public void MapApplicationBoundaries_Should_Map_All_Boundaries()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        var boundaries = new IApplicationBoundary[]
        {
            new FakeBoundary(),
            new FakeBoundary()
        };

        app.MapApplicationBoundaries(boundaries);

        Assert.All(boundaries.Cast<FakeBoundary>(), boundary => Assert.Equal(1, boundary.MapCalls));
    }

    private sealed class FakeBoundary : IApplicationBoundary
    {
        public int RegisterCalls { get; private set; }

        public int MapCalls { get; private set; }

        public void RegisterServices(IServiceCollection services, IConfiguration configuration)
        {
            RegisterCalls++;
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            MapCalls++;
        }
    }
}
