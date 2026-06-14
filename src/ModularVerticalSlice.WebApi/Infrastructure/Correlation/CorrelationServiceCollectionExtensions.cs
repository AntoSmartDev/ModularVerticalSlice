using ModularVerticalSlice.Application.Shared.Observability;

namespace ModularVerticalSlice.WebApi.Infrastructure.Correlation;

/// <summary>Registers correlation ID services and middleware.</summary>
public static class CorrelationServiceCollectionExtensions
{
    /// <summary>Registers <see cref="ICorrelationContext"/> and its WebApi implementation.</summary>
    public static IServiceCollection AddCorrelation(this IServiceCollection services)
    {
        services.AddScoped<CorrelationContext>();
        services.AddScoped<ICorrelationContext>(sp => sp.GetRequiredService<CorrelationContext>());
        return services;
    }

    /// <summary>Adds the correlation ID middleware to the pipeline.</summary>
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
