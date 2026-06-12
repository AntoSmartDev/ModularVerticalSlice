using System.Security.Claims;
using ModularVerticalSlice.Application.Shared.Security;

namespace ModularVerticalSlice.WebApi.Infrastructure.Authentication;

/// <summary>
/// Resolves the current application user from the active HTTP request.
/// </summary>
/// <remarks>
/// This implementation lives in WebApi because modules must not depend on
/// HttpContext directly.
/// </remarks>
public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor) : ICurrentUserContext
{
    /// <inheritdoc />
    public string UserId =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated is true
            ? httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
              httpContextAccessor.HttpContext.User.FindFirstValue("sub") ??
              string.Empty
            : string.Empty;
}
