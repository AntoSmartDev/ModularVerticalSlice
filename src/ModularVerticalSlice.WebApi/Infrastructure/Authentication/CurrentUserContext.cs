using System.Security.Claims;
using ModularVerticalSlice.Application.Shared.Security;

namespace ModularVerticalSlice.WebApi.Infrastructure.Authentication;

/// <summary>
/// Resolves the current application user from the active HTTP request.
/// </summary>
/// <remarks>
/// This implementation lives in WebApi because modules must not depend on
/// HttpContext directly. In development, a stable fallback user keeps the local
/// baseline runnable before the real authentication pipeline is introduced.
/// </remarks>
public sealed class CurrentUserContext(
    IHttpContextAccessor httpContextAccessor,
    IWebHostEnvironment environment) : ICurrentUserContext
{
    /// <inheritdoc />
    public string UserId
    {
        get
        {
            var httpContext = httpContextAccessor.HttpContext;

            var userId =
                httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                httpContext?.User.FindFirstValue("sub") ??
                httpContext?.Request.Headers["X-Demo-UserId"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return userId;
            }

            return environment.IsDevelopment() ? "demo-user" : string.Empty;
        }
    }
}
