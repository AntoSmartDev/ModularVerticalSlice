using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using ModularVerticalSlice.Application.Shared.Modules;

namespace ModularVerticalSlice.Application.Modules.Payments;

/// <summary>
/// Registers services and endpoints exposed by the Payments module.
/// </summary>
/// <remarks>
/// The module class is the entry point used by the WebApi composition root.
/// It must not contain business logic.
/// </remarks>
public sealed class PaymentsModule : IModule
{
    /// <inheritdoc />
    public void RegisterModule(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<PaymentsCircuitBreakerOptions>()
            .Bind(configuration.GetSection(PaymentsCircuitBreakerOptions.SectionName))
            .Validate(
                options => PaymentsCircuitBreakerOptions.Validate(options).Succeeded,
                "Payments circuit-breaker values are invalid.")
            .ValidateOnStart();

        services.AddScoped<IPaymentGateway, FakePaymentGateway>();
    }

    /// <inheritdoc />
    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
    }
}
