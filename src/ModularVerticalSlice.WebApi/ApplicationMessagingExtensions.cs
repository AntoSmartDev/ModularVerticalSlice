using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Payments;
using ModularVerticalSlice.Application.Modules.Payments.Domain;
using ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Notifications;
using ModularVerticalSlice.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RDBMS;

namespace ModularVerticalSlice.WebApi;

/// <summary>
/// Holds the explicit Wolverine messaging configuration used by the host.
/// </summary>
public static class ApplicationMessagingExtensions
{
    /// <summary>
    /// Applies the shared Wolverine messaging baseline for the application host.
    /// </summary>
    /// <param name="options">The Wolverine options being configured.</param>
    /// <param name="configuration">The host configuration.</param>
    /// <param name="configureDbContext">Optional additional DbContext configuration for a host.</param>
    public static void ConfigureApplicationMessaging(
        this WolverineOptions options,
        IConfiguration configuration,
        Action<DbContextOptionsBuilder>? configureDbContext = null)
    {
        var connectionString = configuration.GetRequiredDatabaseConnectionString();
        var paymentsCircuitBreaker = GetPaymentsCircuitBreakerOptions(configuration);

        options.Services.AddDbContextWithWolverineIntegration<AppDbContext>(builder =>
        {
            builder.UseNpgsql(connectionString);
            configureDbContext?.Invoke(builder);
        });
        options.UseRuntimeCompilation();
        options.Discovery.IncludeAssembly(typeof(BookingsModule).Assembly);
        options.Policies.AutoApplyTransactions();
        options.AddSagaType<BookingLifecycleSaga>("booking_lifecycle_sagas");
        PaymentsRuntimeRecoveryPolicies.Configure(options);
        NotificationsRuntimeRecoveryPolicies.Configure(options);
        options.LocalQueueFor<BookingConfirmedEvent>().UseDurableInbox();
        options.PublishMessage<ProcessPaymentCommand>()
            .ToLocalQueue(PaymentsCircuitBreakerOptions.QueueName);
        options.LocalQueue(PaymentsCircuitBreakerOptions.QueueName)
            .UseDurableInbox()
            .CircuitBreaker(circuit =>
            {
                circuit.MinimumThreshold = paymentsCircuitBreaker.MinimumThreshold;
                circuit.FailurePercentageThreshold = paymentsCircuitBreaker.FailurePercentageThreshold;
                circuit.TrackingPeriod = paymentsCircuitBreaker.TrackingPeriod;
                circuit.PauseTime = paymentsCircuitBreaker.PauseTime;
                circuit.Include<PaymentTechnicalFailureException>(
                    PaymentsRuntimeRecoveryPolicies.ShouldAffectCircuitBreaker);
            });
        options
            .PersistMessagesWithPostgresql(connectionString, "messaging")
            .EnableMessageTransport(_ => { });
    }

    private static PaymentsCircuitBreakerOptions GetPaymentsCircuitBreakerOptions(
        IConfiguration configuration)
    {
        var circuitBreaker = configuration
            .GetRequiredSection(PaymentsCircuitBreakerOptions.SectionName)
            .Get<PaymentsCircuitBreakerOptions>()
            ?? throw new OptionsValidationException(
                PaymentsCircuitBreakerOptions.SectionName,
                typeof(PaymentsCircuitBreakerOptions),
                ["Payments circuit-breaker configuration is required."]);

        var paymentWindow = configuration.GetValue<TimeSpan>("Bookings:Lifecycle:PaymentWindow");
        var validation = PaymentsCircuitBreakerOptions.Validate(circuitBreaker, paymentWindow);

        if (validation.Failed)
        {
            throw new OptionsValidationException(
                PaymentsCircuitBreakerOptions.SectionName,
                typeof(PaymentsCircuitBreakerOptions),
                validation.Failures);
        }

        return circuitBreaker;
    }
}
