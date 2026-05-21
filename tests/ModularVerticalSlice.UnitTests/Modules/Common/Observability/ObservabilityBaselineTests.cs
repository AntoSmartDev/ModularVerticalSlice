using ModularVerticalSlice.Modules.Shared.Observability;

namespace ModularVerticalSlice.UnitTests.Modules.Common.Observability;

/// <summary>
/// Verifies the minimal shared observability abstractions used by modules.
/// </summary>
public class ObservabilityBaselineTests
{
    /// <summary>
    /// Verifies that the correlation context contract exposes a read-only correlation identifier.
    /// </summary>
    [Fact]
    public void ICorrelationContext_Should_Expose_ReadOnly_CorrelationId()
    {
        var property = typeof(ICorrelationContext).GetProperty(nameof(ICorrelationContext.CorrelationId));

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }
}
