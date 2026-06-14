using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ModularVerticalSlice.WebApi.Infrastructure.Observability;

/// <summary>
/// Registers the host-level OpenTelemetry tracing baseline for the WebApi.
/// </summary>
public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds the shared OpenTelemetry tracing baseline used by the WebApi host.
    /// </summary>
    public static IServiceCollection AddWebApiObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var serviceName = configuration["OpenTelemetry:ServiceName"] ?? environment.ApplicationName;

        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                if (environment.IsDevelopment())
                    tracing.AddConsoleExporter();
                var otlpEndpoint = configuration["OpenTelemetry:Otlp:Endpoint"];
                if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                    tracing.AddOtlpExporter(otlp => otlp.Endpoint = new Uri(otlpEndpoint));
            });

        return services;
    }
}
