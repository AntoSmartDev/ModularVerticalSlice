using Microsoft.AspNetCore.Authentication;

namespace ModularVerticalSlice.WebApi.Infrastructure.Authentication;

/// <summary>
/// Configures the principal produced by the local fake authentication scheme.
/// </summary>
public sealed class FakeAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// Gets or sets the stable subject identifier.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the role claims.
    /// </summary>
    public string[] Roles { get; set; } = [];

    /// <summary>
    /// Gets or sets the scope claims.
    /// </summary>
    public string[] Scopes { get; set; } = [];
}
