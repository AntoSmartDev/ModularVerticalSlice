using ModularVerticalSlice.Application.Shared.Observability;

namespace ModularVerticalSlice.WebApi.Infrastructure.Correlation;

internal sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
