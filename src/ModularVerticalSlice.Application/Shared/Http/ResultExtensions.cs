using Microsoft.AspNetCore.Http;
using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Modules.Shared.Http;

/// <summary>
/// Maps application results to consistent HTTP responses.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps a successful or failed generic result to an HTTP response.
    /// </summary>
    /// <typeparam name="TValue">The returned value type.</typeparam>
    /// <param name="result">The application result.</param>
    /// <returns>The mapped HTTP response.</returns>
    public static IResult ToHttpResponse<TValue>(this Result<TValue> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.Ok(result.Value)
            : MapError(result.Error);
    }

    /// <summary>
    /// Maps a successful or failed non-generic result to an HTTP response.
    /// </summary>
    /// <param name="result">The application result.</param>
    /// <returns>The mapped HTTP response.</returns>
    public static IResult ToHttpResponse(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            ? Results.NoContent()
            : MapError(result.Error);
    }

    private static IResult MapError(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status422UnprocessableEntity,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        if (error.Type is ErrorType.Validation &&
            error.ValidationErrors is not null)
        {
            return Results.ValidationProblem(
                errors: error.ValidationErrors,
                title: error.Code,
                detail: error.Description,
                statusCode: statusCode);
        }

        return Results.Problem(
            title: error.Code,
            detail: error.Description,
            statusCode: statusCode);
    }
}
