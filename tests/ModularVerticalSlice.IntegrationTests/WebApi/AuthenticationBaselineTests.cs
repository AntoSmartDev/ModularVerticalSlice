using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using ModularVerticalSlice.WebApi.Infrastructure.Authentication;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

public sealed class AuthenticationBaselineTests
{
    [Fact]
    public async Task FakeAuthentication_Should_Produce_Configured_Principal()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Scheme"] = FakeAuthenticationDefaults.Scheme,
            ["Authentication:Fake:UserId"] = "user-42",
            ["Authentication:Fake:Name"] = "Test User",
            ["Authentication:Fake:Roles:0"] = "operator",
            ["Authentication:Fake:Scopes:0"] = "bookings.read"
        });
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebApiAuthentication(configuration, new TestWebHostEnvironment("Test"));
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider };

        var result = await context.AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("user-42", result.Principal!.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("Test User", result.Principal.FindFirstValue(ClaimTypes.Name));
        Assert.Contains(result.Principal.Claims, claim => claim is { Type: ClaimTypes.Role, Value: "operator" });
        Assert.Contains(result.Principal.Claims, claim => claim is { Type: "scope", Value: "bookings.read" });
    }

    [Fact]
    public void FakeAuthentication_Should_Fail_Fast_In_Production()
    {
        var configuration = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Authentication:Scheme"] = FakeAuthenticationDefaults.Scheme
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddWebApiAuthentication(
                configuration,
                new TestWebHostEnvironment("Production")));

        Assert.Equal("Fake authentication cannot be enabled in Production.", exception.Message);
    }

    [Fact]
    public void CurrentUserContext_Should_Resolve_Only_Authenticated_Principal_Claims()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Demo-UserId"] = "header-user";
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "principal-user")],
            FakeAuthenticationDefaults.Scheme));
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        var currentUser = new CurrentUserContext(accessor);

        Assert.Equal("principal-user", currentUser.UserId);

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "unauthenticated-user")]));

        Assert.Equal(string.Empty, currentUser.UserId);
    }

    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = nameof(AuthenticationBaselineTests);

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string WebRootPath { get; set; } = string.Empty;

        public string EnvironmentName { get; set; } = environmentName;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
