using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Shared.Http;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.UnitTests.Modules.Common.Http;

/// <summary>
/// Verifies the HTTP mapping behavior of <see cref="ModularVerticalSlice.Application.Shared.Http.ResultExtensions" />.
/// </summary>
public class ResultExtensionsTests
{
    /// <summary>
    /// Verifies that a successful generic result is mapped to HTTP 200.
    /// </summary>
    [Fact]
    public async Task ResultExtensions_Should_Map_Successful_ResultOfT_To_200()
    {
        Result<string> result = "booking-123";

        var response = await ExecuteAsync(result.ToHttpResponse());

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal("\"booking-123\"", response.Body);
    }

    /// <summary>
    /// Verifies that a successful non-generic result is mapped to HTTP 204.
    /// </summary>
    [Fact]
    public async Task ResultExtensions_Should_Map_Successful_Result_To_204()
    {
        var result = Result.Success();

        var response = await ExecuteAsync(result.ToHttpResponse());

        Assert.Equal(StatusCodes.Status204NoContent, response.StatusCode);
    }

    /// <summary>
    /// Verifies that not-found errors are mapped to HTTP 404.
    /// </summary>
    [Fact]
    public async Task ResultExtensions_Should_Map_NotFound_To_404()
    {
        Result<string> result = Error.NotFound(
            "Catalog.EventNotFound",
            "The requested event was not found.");

        var response = await ExecuteAsync(result.ToHttpResponse());

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal("Catalog.EventNotFound", GetStringProperty(response.ProblemDetails, "title"));
        Assert.Equal("The requested event was not found.", GetStringProperty(response.ProblemDetails, "detail"));
    }

    /// <summary>
    /// Verifies that validation errors are mapped to HTTP 422.
    /// </summary>
    [Fact]
    public async Task ResultExtensions_Should_Map_Validation_To_422()
    {
        Result<string> result = Error.Validation(
            "Catalog.InvalidTicketQuantity",
            "The request is invalid.",
            new Dictionary<string, string[]>
            {
                ["Quantity"] = ["Quantity must be greater than zero."]
            });

        var response = await ExecuteAsync(result.ToHttpResponse());

        Assert.Equal(StatusCodes.Status422UnprocessableEntity, response.StatusCode);
        Assert.Equal("Catalog.InvalidTicketQuantity", GetStringProperty(response.ProblemDetails, "title"));
        Assert.Equal("The request is invalid.", GetStringProperty(response.ProblemDetails, "detail"));
        Assert.Equal(
            "Quantity must be greater than zero.",
            GetValidationError(response.ProblemDetails, "Quantity"));
    }

    /// <summary>
    /// Verifies that conflict errors are mapped to HTTP 409.
    /// </summary>
    [Fact]
    public async Task ResultExtensions_Should_Map_Conflict_To_409()
    {
        Result<string> result = Error.Conflict(
            "Catalog.NotEnoughTickets",
            "Not enough tickets are available.");

        var response = await ExecuteAsync(result.ToHttpResponse());

        Assert.Equal(StatusCodes.Status409Conflict, response.StatusCode);
        Assert.Equal("Catalog.NotEnoughTickets", GetStringProperty(response.ProblemDetails, "title"));
        Assert.Equal("Not enough tickets are available.", GetStringProperty(response.ProblemDetails, "detail"));
    }

    /// <summary>
    /// Verifies that unauthorized errors are mapped to HTTP 401.
    /// </summary>
    [Fact]
    public async Task ResultExtensions_Should_Map_Unauthorized_To_401()
    {
        Result<string> result = Error.Unauthorized(
            "Auth.MissingUser",
            "The caller is not authenticated.");

        var response = await ExecuteAsync(result.ToHttpResponse());

        Assert.Equal(StatusCodes.Status401Unauthorized, response.StatusCode);
        Assert.Equal("Auth.MissingUser", GetStringProperty(response.ProblemDetails, "title"));
    }

    /// <summary>
    /// Verifies that forbidden errors are mapped to HTTP 403.
    /// </summary>
    [Fact]
    public async Task ResultExtensions_Should_Map_Forbidden_To_403()
    {
        Result<string> result = Error.Forbidden(
            "Auth.ForbiddenAction",
            "The caller is not allowed.");

        var response = await ExecuteAsync(result.ToHttpResponse());

        Assert.Equal(StatusCodes.Status403Forbidden, response.StatusCode);
        Assert.Equal("Auth.ForbiddenAction", GetStringProperty(response.ProblemDetails, "title"));
    }

    /// <summary>
    /// Verifies that generic failures fall back to HTTP 500.
    /// </summary>
    [Fact]
    public async Task ResultExtensions_Should_Map_Generic_Failure_To_500()
    {
        Result<string> result = Error.Failure(
            "General.UnexpectedFailure",
            "An unexpected failure occurred.");

        var response = await ExecuteAsync(result.ToHttpResponse());

        Assert.Equal(StatusCodes.Status500InternalServerError, response.StatusCode);
        Assert.Equal("General.UnexpectedFailure", GetStringProperty(response.ProblemDetails, "title"));
    }

    private static async Task<HttpExecutionResult> ExecuteAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Body = new MemoryStream();
        httpContext.RequestServices = services.BuildServiceProvider();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;

        using var reader = new StreamReader(httpContext.Response.Body);
        var body = await reader.ReadToEndAsync();

        JsonElement? problemDetails = null;

        if (!string.IsNullOrWhiteSpace(body) &&
            body.TrimStart().StartsWith('{'))
        {
            using var document = JsonDocument.Parse(body);
            problemDetails = document.RootElement.Clone();
        }

        return new HttpExecutionResult(
            httpContext.Response.StatusCode,
            body,
            problemDetails);
    }

    private static string? GetStringProperty(JsonElement? problemDetails, string propertyName)
    {
        if (problemDetails is null ||
            !problemDetails.Value.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.GetString();
    }

    private static string? GetValidationError(JsonElement? problemDetails, string fieldName)
    {
        if (problemDetails is null ||
            !problemDetails.Value.TryGetProperty("errors", out var errors) ||
            !errors.TryGetProperty(fieldName, out var fieldErrors) ||
            fieldErrors.ValueKind != JsonValueKind.Array ||
            fieldErrors.GetArrayLength() == 0)
        {
            return null;
        }

        return fieldErrors[0].GetString();
    }

    private sealed record HttpExecutionResult(
        int StatusCode,
        string Body,
        JsonElement? ProblemDetails);
}
