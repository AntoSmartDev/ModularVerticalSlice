using Microsoft.AspNetCore.Routing;

namespace ModularVerticalSlice.Application.Shared.Composition;

/// <summary>
/// Provides endpoint mapping helpers for application boundaries.
/// </summary>
public static class ApplicationBoundaryEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the endpoints exposed by each application boundary.
    /// </summary>
    /// <param name="endpoints">The route builder used by the application host.</param>
    /// <param name="boundaries">The boundaries to map.</param>
    /// <returns>The same route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapApplicationBoundaries(
        this IEndpointRouteBuilder endpoints,
        IEnumerable<IApplicationBoundary> boundaries)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentNullException.ThrowIfNull(boundaries);

        foreach (var boundary in boundaries)
        {
            boundary.MapEndpoints(endpoints);
        }

        return endpoints;
    }
}
