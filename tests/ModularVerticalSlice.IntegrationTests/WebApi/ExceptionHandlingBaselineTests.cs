using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

/// <summary>
/// Proves that unhandled exceptions in the HTTP pipeline produce a structured
/// Problem Details response and never expose internal details as raw text.
/// </summary>
public sealed class ExceptionHandlingBaselineTests
{
    [Fact]
    public async Task UnhandledException_Returns_ProblemDetails_Response()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/throw", IResult () => throw new InvalidOperationException("integration test exception"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var client = app.GetTestClient();

            var response = await client.GetAsync(
                "/throw",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Equal(
                "application/problem+json",
                response.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task ExceptionHandler_Does_Not_Affect_Normal_Responses()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddProblemDetails();

        var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/ok", () => Results.Ok("healthy"));

        await app.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            var client = app.GetTestClient();

            var response = await client.GetAsync(
                "/ok",
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await app.StopAsync(TestContext.Current.CancellationToken);
        }
    }
}
