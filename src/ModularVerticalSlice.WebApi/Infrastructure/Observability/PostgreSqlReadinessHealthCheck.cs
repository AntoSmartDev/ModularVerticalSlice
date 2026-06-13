using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ModularVerticalSlice.Persistence;

namespace ModularVerticalSlice.WebApi.Infrastructure.Observability;

/// <summary>
/// Reports readiness based on the application's PostgreSQL connectivity.
/// The Wolverine message store shares the same connection string, so
/// <see cref="AppDbContext"/> connectivity is the faithful minimum proxy for
/// "the database capability the running application requires".
/// </summary>
internal sealed class PostgreSqlReadinessHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public PostgreSqlReadinessHealthCheck(AppDbContext dbContext) => _dbContext = dbContext;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
            : HealthCheckResult.Unhealthy("PostgreSQL is not reachable.");
    }
}
