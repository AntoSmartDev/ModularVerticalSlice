using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

public sealed class HealthChecksBaselineTests
{
    [Fact]
    public async Task Live_Endpoint_Returns_200()
    {
        var app = BuildApp();

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var response = await app.GetTestClient()
                .GetAsync("/health/live", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Ready_Endpoint_Returns_200()
    {
        var app = BuildApp();

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var response = await app.GetTestClient()
                .GetAsync("/health/ready", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Health_Endpoints_Do_Not_Require_Authentication()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHealthChecks();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapHealthChecks("/health/live").AllowAnonymous();
        app.MapHealthChecks("/health/ready").AllowAnonymous();

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var client = app.GetTestClient();

            var live = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);
            var ready = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static WebApplication BuildApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddHealthChecks();

        var app = builder.Build();
        app.MapHealthChecks("/health/live").AllowAnonymous();
        app.MapHealthChecks("/health/ready").AllowAnonymous();
        return app;
    }
}
