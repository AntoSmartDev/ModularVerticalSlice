using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Shared.Modules;

namespace ModularVerticalSlice.UnitTests.Modules.Common.Modules;

/// <summary>
/// Verifies the baseline behavior of module registration and endpoint mapping extensions.
/// </summary>
public class ModuleRegistrationExtensionsTests
{
    /// <summary>
    /// Verifies that service registration delegates to every supplied module.
    /// </summary>
    [Fact]
    public void AddApplicationModules_Should_Register_All_Modules()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var modules = new IModule[]
        {
            new FakeModule(),
            new FakeModule()
        };

        services.AddApplicationModules(configuration, modules);

        Assert.All(modules.Cast<FakeModule>(), module => Assert.Equal(1, module.RegisterCalls));
    }

    /// <summary>
    /// Verifies that endpoint mapping delegates to every supplied module.
    /// </summary>
    [Fact]
    public void MapApplicationModules_Should_Map_All_Modules()
    {
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();
        var modules = new IModule[]
        {
            new FakeModule(),
            new FakeModule()
        };

        app.MapApplicationModules(modules);

        Assert.All(modules.Cast<FakeModule>(), module => Assert.Equal(1, module.MapCalls));
    }

    private sealed class FakeModule : IModule
    {
        public int RegisterCalls { get; private set; }

        public int MapCalls { get; private set; }

        public void RegisterModule(IServiceCollection services, IConfiguration configuration)
        {
            RegisterCalls++;
        }

        public void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            MapCalls++;
        }
    }
}
