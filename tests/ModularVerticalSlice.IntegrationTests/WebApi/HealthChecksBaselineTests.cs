using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi.Infrastructure.HealthChecks;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

public sealed class HealthChecksBaselineTests
{
    // A reachable PostgreSQL refuses connections quickly on this unused local port,
    // so the readiness failure proof stays fast and deterministic.
    private const string UnreachableConnectionString =
        "Host=localhost;Port=59999;Database=modularverticalslice;Username=postgres;Password=postgres";

    [Fact]
    public async Task Live_Endpoint_Is_Process_Only_And_Returns_200_Even_When_Postgres_Is_Unreachable()
    {
        var app = BuildApp(UnreachableConnectionString);

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var response = await app.GetTestClient()
                .GetAsync(HealthChecksExtensions.LivePath, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Ready_Endpoint_Returns_503_When_Postgres_Is_Unreachable()
    {
        var app = BuildApp(UnreachableConnectionString);

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var response = await app.GetTestClient()
                .GetAsync(HealthChecksExtensions.ReadyPath, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task Ready_Endpoint_Returns_200_When_Postgres_Is_Reachable()
    {
        var app = BuildApp(ReachableConnectionString());

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var response = await app.GetTestClient()
                .GetAsync(HealthChecksExtensions.ReadyPath, TestContext.Current.CancellationToken);

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
        builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(ReachableConnectionString()));
        builder.Services.AddApplicationHealthChecks();
        builder.Services.AddAuthentication();
        builder.Services.AddAuthorization();

        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapApplicationHealthEndpoints();

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var client = app.GetTestClient();

            var live = await client.GetAsync(
                HealthChecksExtensions.LivePath, TestContext.Current.CancellationToken);
            var ready = await client.GetAsync(
                HealthChecksExtensions.ReadyPath, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static WebApplication BuildApp(string connectionString)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connectionString));
        builder.Services.AddApplicationHealthChecks();

        var app = builder.Build();
        app.MapApplicationHealthEndpoints();
        return app;
    }

    private static string ReachableConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.Development.json", optional: false)
            .Build();

        return configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is required for the readiness recovery proof.");
    }
}
