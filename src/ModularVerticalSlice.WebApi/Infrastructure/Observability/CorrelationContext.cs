using ModularVerticalSlice.Application.Shared.Observability;

namespace ModularVerticalSlice.WebApi.Infrastructure.Observability;

internal sealed class CorrelationContext : ICorrelationContext
{
    public string CorrelationId { get; set; } = string.Empty;
}
