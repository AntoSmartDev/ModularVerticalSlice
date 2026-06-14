using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking;
using ModularVerticalSlice.Application.Modules.Bookings.Features.GetBookingDetails;
using ModularVerticalSlice.Application.Modules.Bookings.Features.GetCustomerBookings;
using ModularVerticalSlice.Application.Shared.Security;
using ModularVerticalSlice.WebApi.Infrastructure.Authorization;

namespace ModularVerticalSlice.IntegrationTests.WebApi;

/// <summary>
/// Verifies that authorization policies are registered with the correct scope-claim
/// requirements and that Bookings endpoints expose the expected policy metadata.
/// </summary>
public sealed class AuthorizationBaselineTests
{
    /// <summary>
    /// Verifies that <c>bookings.read</c> policy requires the <c>scope</c> claim
    /// with the documented value.
    /// </summary>
    [Fact]
    public void BookingsRead_Policy_Should_Require_Scope_Claim()
    {
        var services = new ServiceCollection();
        services.AddWebApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        var policy = options.GetPolicy(AuthorizationPolicies.BookingsRead);

        Assert.NotNull(policy);
        var claimsReq = Assert.Single(
            policy.Requirements.OfType<ClaimsAuthorizationRequirement>());
        Assert.Equal("scope", claimsReq.ClaimType);
        Assert.Contains(AuthorizationPolicies.BookingsRead, claimsReq.AllowedValues!);
    }

    /// <summary>
    /// Verifies that <c>bookings.write</c> policy requires the <c>scope</c> claim
    /// with the documented value.
    /// </summary>
    [Fact]
    public void BookingsWrite_Policy_Should_Require_Scope_Claim()
    {
        var services = new ServiceCollection();
        services.AddWebApiAuthorization();
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        var policy = options.GetPolicy(AuthorizationPolicies.BookingsWrite);

        Assert.NotNull(policy);
        var claimsReq = Assert.Single(
            policy.Requirements.OfType<ClaimsAuthorizationRequirement>());
        Assert.Equal("scope", claimsReq.ClaimType);
        Assert.Contains(AuthorizationPolicies.BookingsWrite, claimsReq.AllowedValues!);
    }

    /// <summary>
    /// Verifies that the three Bookings endpoints expose the correct
    /// <see cref="IAuthorizeData"/> policy-name metadata set by
    /// <c>RequireAuthorization</c>.
    /// </summary>
    [Fact]
    public void Bookings_Endpoints_Should_Expose_RequireAuthorization_Metadata()
    {
        var app = WebApplication.Create();
        GetCustomerBookingsEndpoint.Map(app);
        GetBookingDetailsEndpoint.Map(app);
        CreateBookingEndpoint.Map(app);

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        AssertPolicyOnEndpoint(
            endpoints,
            HttpMethods.Get,
            "/api/v1/bookings/",
            AuthorizationPolicies.BookingsRead);

        AssertPolicyOnEndpoint(
            endpoints,
            HttpMethods.Get,
            "/api/v1/bookings/{id:guid}",
            AuthorizationPolicies.BookingsRead);

        AssertPolicyOnEndpoint(
            endpoints,
            HttpMethods.Post,
            "/api/v1/bookings/",
            AuthorizationPolicies.BookingsWrite);
    }

    private static void AssertPolicyOnEndpoint(
        IReadOnlyList<RouteEndpoint> endpoints,
        string httpMethod,
        string routePattern,
        string expectedPolicy)
    {
        var match = endpoints.FirstOrDefault(e =>
            e.RoutePattern.RawText == routePattern &&
            e.Metadata.GetMetadata<IHttpMethodMetadata>() is { } m &&
            m.HttpMethods.Contains(httpMethod));

        Assert.True(
            match is not null,
            $"No endpoint found for {httpMethod} {routePattern}");

        var authorizeData = match!.Metadata.GetMetadata<IAuthorizeData>();
        Assert.NotNull(authorizeData);
        Assert.Equal(expectedPolicy, authorizeData.Policy);
    }
}
