using ModularVerticalSlice.Application.Shared.Security;

namespace ModularVerticalSlice.UnitTests.Modules.Common.Security;

/// <summary>
/// Verifies the minimal shared security abstractions used by modules.
/// </summary>
public class SecurityBaselineTests
{
    /// <summary>
    /// Verifies that the current user contract exposes a read-only user identifier.
    /// </summary>
    [Fact]
    public void ICurrentUserContext_Should_Expose_ReadOnly_UserId()
    {
        var property = typeof(ICurrentUserContext).GetProperty(nameof(ICurrentUserContext.UserId));

        Assert.NotNull(property);
        Assert.Equal(typeof(string), property.PropertyType);
        Assert.True(property.CanRead);
        Assert.False(property.CanWrite);
    }

    /// <summary>
    /// Verifies that the baseline authorization policy names match the documented blueprint values.
    /// </summary>
    [Fact]
    public void AuthorizationPolicies_Should_Expose_Stable_Booking_Policy_Names()
    {
        Assert.Equal("bookings.read", AuthorizationPolicies.BookingsRead);
        Assert.Equal("bookings.write", AuthorizationPolicies.BookingsWrite);
    }
}
