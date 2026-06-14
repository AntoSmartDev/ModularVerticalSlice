using ModularVerticalSlice.Application.Shared.Security;

namespace ModularVerticalSlice.WebApi.Infrastructure.Authorization;

/// <summary>
/// Configures the WebApi authorization boundary.
/// </summary>
public static class AuthorizationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the scope-based authorization policies for the application modules.
    /// </summary>
    public static IServiceCollection AddWebApiAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.BookingsRead,
                policy => policy.RequireClaim("scope", AuthorizationPolicies.BookingsRead));

            options.AddPolicy(
                AuthorizationPolicies.BookingsWrite,
                policy => policy.RequireClaim("scope", AuthorizationPolicies.BookingsWrite));
        });

        return services;
    }
}
