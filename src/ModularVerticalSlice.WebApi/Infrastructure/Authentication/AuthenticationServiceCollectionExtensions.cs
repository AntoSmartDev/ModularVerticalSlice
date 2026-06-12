using Microsoft.AspNetCore.Authentication;

namespace ModularVerticalSlice.WebApi.Infrastructure.Authentication;

/// <summary>
/// Configures the WebApi authentication boundary.
/// </summary>
public static class AuthenticationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the selected authentication scheme and rejects FakeAuth in Production.
    /// </summary>
    public static IServiceCollection AddWebApiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var selectedScheme = configuration["Authentication:Scheme"];
        var isFakeSelected = string.Equals(
            selectedScheme,
            FakeAuthenticationDefaults.Scheme,
            StringComparison.OrdinalIgnoreCase);
        var defaultScheme = isFakeSelected ? FakeAuthenticationDefaults.Scheme : selectedScheme;

        if (environment.IsProduction() && isFakeSelected)
        {
            throw new InvalidOperationException("Fake authentication cannot be enabled in Production.");
        }

        AuthenticationBuilder authentication = services.AddAuthentication(options =>
        {
            if (!string.IsNullOrWhiteSpace(defaultScheme))
            {
                options.DefaultAuthenticateScheme = defaultScheme;
                options.DefaultChallengeScheme = defaultScheme;
            }
        });

        if (isFakeSelected)
        {
            authentication.AddScheme<FakeAuthenticationOptions, FakeAuthenticationHandler>(
                FakeAuthenticationDefaults.Scheme,
                options => configuration.GetSection("Authentication:Fake").Bind(options));
        }

        return services;
    }
}
