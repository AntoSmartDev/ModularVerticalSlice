using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ModularVerticalSlice.WebApi.Infrastructure.HealthChecks;

/// <summary>
/// Wires the application's liveness and readiness health contract so the host
/// and its verification share exactly the same registration and mapping.
/// </summary>
public static class HealthChecksExtensions
{
    /// <summary>Tag identifying readiness (dependency-aware) checks.</summary>
    public const string ReadyTag = "ready";

    /// <summary>Liveness probe path. Process-only: it runs no dependency checks.</summary>
    public const string LivePath = "/health/live";

    /// <summary>Readiness probe path. Runs only <see cref="ReadyTag"/>-tagged checks.</summary>
    public const string ReadyPath = "/health/ready";

    /// <summary>
    /// Registers the application readiness checks. Liveness intentionally
    /// registers no check and is expressed purely at mapping time.
    /// </summary>
    public static IServiceCollection AddApplicationHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<PostgreSqlReadinessHealthCheck>(
                "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag]);

        return services;
    }

    /// <summary>
    /// Maps the liveness and readiness endpoints. Liveness selects no checks so
    /// it reports process health only; readiness selects only readiness checks.
    /// </summary>
    public static IEndpointRouteBuilder MapApplicationHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks(LivePath, new HealthCheckOptions
        {
            Predicate = _ => false
        }).AllowAnonymous();

        endpoints.MapHealthChecks(ReadyPath, new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag)
        }).AllowAnonymous();

        return endpoints;
    }
}
