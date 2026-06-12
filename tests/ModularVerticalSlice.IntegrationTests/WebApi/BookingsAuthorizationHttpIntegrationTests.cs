using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModularVerticalSlice.Application.Modules.Bookings.Features.GetCustomerBookings;
using ModularVerticalSlice.Persistence;
using ModularVerticalSlice.WebApi.Infrastructure.Authentication;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

/// <summary>
/// Proves that the Bookings HTTP endpoints enforce the authorization boundary:
/// unauthenticated requests are challenged (401), authenticated requests without
/// the required scope are forbidden (403), and a fully-authorized principal
/// reaches the handler with CurrentUserContext resolved correctly.
/// Catalog public-read endpoints remain accessible regardless of Bookings scope.
/// </summary>
public sealed class BookingsAuthorizationHttpIntegrationTests
{
    [Fact]
    public async Task BookingsRead_Returns_401_When_Request_Is_Unauthenticated()
    {
        await using var factory = new UnauthenticatedWebApplicationFactory();
        var client = factory.CreateClient();
        await MigrateAsync(factory);

        var response = await client.GetAsync(
            "/api/v1/bookings/",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BookingsWrite_Returns_401_When_Request_Is_Unauthenticated()
    {
        await using var factory = new UnauthenticatedWebApplicationFactory();
        var client = factory.CreateClient();
        await MigrateAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/bookings/",
            new { },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task BookingsRead_Returns_403_When_Principal_Lacks_Scope()
    {
        await using var factory = new NoScopeWebApplicationFactory();
        var client = factory.CreateClient();
        await MigrateAsync(factory);

        var response = await client.GetAsync(
            "/api/v1/bookings/",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task BookingsWrite_Returns_403_When_Principal_Lacks_Scope()
    {
        await using var factory = new NoScopeWebApplicationFactory();
        var client = factory.CreateClient();
        await MigrateAsync(factory);

        var response = await client.PostAsJsonAsync(
            "/api/v1/bookings/",
            new { },
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CatalogEvents_Returns_200_When_Principal_Lacks_Bookings_Scope()
    {
        await using var factory = new NoScopeWebApplicationFactory();
        var client = factory.CreateClient();
        await MigrateAsync(factory);

        var response = await client.GetAsync(
            "/api/v1/events/",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task BookingsRead_Returns_200_And_CurrentUserContext_Resolves_When_Authorized()
    {
        await using var factory = new DevWebApplicationFactory();
        var client = factory.CreateClient();
        await MigrateAsync(factory);

        var response = await client.GetAsync(
            "/api/v1/bookings/",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bookings = await response.Content.ReadFromJsonAsync<CustomerBookingReadModel[]>(
            TestContext.Current.CancellationToken);
        Assert.NotNull(bookings);
    }

    private sealed class DevWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
        }
    }

    private sealed class UnauthenticatedWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
                services.Configure<FakeAuthenticationOptions>(
                    FakeAuthenticationDefaults.Scheme,
                    opts => opts.UserId = string.Empty));
        }
    }

    private sealed class NoScopeWebApplicationFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureServices(services =>
                services.Configure<FakeAuthenticationOptions>(
                    FakeAuthenticationDefaults.Scheme,
                    opts =>
                    {
                        opts.UserId = "noscope-user";
                        opts.Scopes = [];
                    }));
        }
    }

    private static async Task MigrateAsync(WebApplicationFactory<Program> factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider
            .GetRequiredService<AppDbContext>()
            .Database
            .MigrateAsync();
    }
}
