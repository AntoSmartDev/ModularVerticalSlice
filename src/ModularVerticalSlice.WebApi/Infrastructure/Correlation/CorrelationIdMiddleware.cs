using ModularVerticalSlice.Application.Shared.Observability;

namespace ModularVerticalSlice.WebApi.Infrastructure.Correlation;

internal sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        CorrelationContext correlationContext,
        ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? context.TraceIdentifier;

        correlationContext.CorrelationId = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object>
        {
            [CorrelationLoggingMiddleware.CorrelationIdKey] = correlationId
        }))
        {
            await _next(context);
        }
    }
}
