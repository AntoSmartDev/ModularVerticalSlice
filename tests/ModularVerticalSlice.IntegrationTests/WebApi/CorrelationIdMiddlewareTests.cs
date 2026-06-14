using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Shared.Observability;
using ModularVerticalSlice.WebApi.Infrastructure.Correlation;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

public sealed class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task IncomingHeader_IsEchoedBackUnchanged()
    {
        var app = BuildApp(a => a.MapGet("/ok", () => Results.Ok()));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/ok");
            request.Headers.Add("X-Correlation-Id", "my-test-correlation-id");

            var response = await app.GetTestClient()
                .SendAsync(request, TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
            Assert.Equal("my-test-correlation-id", values.Single());
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task MissingHeader_GeneratesNonEmptyCorrelationId()
    {
        var app = BuildApp(a => a.MapGet("/ok", () => Results.Ok()));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var response = await app.GetTestClient()
                .GetAsync("/ok", TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.TryGetValues("X-Correlation-Id", out var values));
            Assert.False(string.IsNullOrEmpty(values.Single()));
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ICorrelationContext_ReturnsIncomingCorrelationId()
    {
        var app = BuildApp(a =>
            a.MapGet("/ctx", (ICorrelationContext ctx) => Results.Ok(ctx.CorrelationId)));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/ctx");
            request.Headers.Add("X-Correlation-Id", "ctx-test-id");

            var response = await app.GetTestClient()
                .SendAsync(request, TestContext.Current.CancellationToken);

            var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("ctx-test-id", body);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    private static WebApplication BuildApp(Action<WebApplication> configure)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddCorrelation();

        var app = builder.Build();
        app.UseCorrelationId();
        configure(app);
        return app;
    }
}
