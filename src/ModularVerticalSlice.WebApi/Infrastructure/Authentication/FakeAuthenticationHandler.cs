using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace ModularVerticalSlice.WebApi.Infrastructure.Authentication;

/// <summary>
/// Produces a deterministic local principal from explicit configuration.
/// </summary>
public sealed class FakeAuthenticationHandler(
    IOptionsMonitor<FakeAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<FakeAuthenticationOptions>(options, logger, encoder)
{
    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrWhiteSpace(Options.UserId))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "Fake authentication requires a configured user id."));
        }

        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, Options.UserId),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(Options.Name) ? Options.UserId : Options.Name)
        ];

        claims.AddRange(Options.Roles
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(Options.Scopes
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => new Claim("scope", scope)));

        var identity = new ClaimsIdentity(claims, FakeAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, FakeAuthenticationDefaults.Scheme);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
