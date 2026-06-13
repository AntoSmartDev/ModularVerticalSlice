using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModularVerticalSlice.Application.Modules.Bookings.Messages;
using ModularVerticalSlice.Application.Modules.Catalog.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Persistence;
using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;
using Wolverine;
using Wolverine.Attributes;

namespace ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

/// <summary>
/// Handles the baseline payment-processing workflow for the Payments module.
/// </summary>
/// <remarks>
/// This handler keeps the first release deliberately small: it evaluates a
/// deterministic business outcome, creates the minimal payment record and
/// publishes the resulting success or business-failure event. Technical
/// failures are surfaced as a dedicated exception so Wolverine can own retry
/// semantics without conflating them with business outcomes.
/// </remarks>
public sealed class PaymentProcessingHandler(
    IPaymentWriteDbContextSlice writeDb,
    IPaymentGateway paymentGateway,
    IMessageBus bus,
    TimeProvider timeProvider,
    ILogger<PaymentProcessingHandler> logger)
{
    /// <summary>
    /// Handles the baseline ProcessPayment command.
    /// </summary>
    [WolverineHandler]
    public async Task<Result> HandleProcessPayment(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var validation = PaymentProcessingValidators.Validate(command);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (timeProvider.GetUtcNow() >= command.PaymentDeadline)
        {
            return Result.Failure(
                Error.Conflict(
                    "Payments.PaymentWindowExpired",
                    "The booking payment window has elapsed."));
        }

        var eligibility = await bus.InvokeAsync<Result>(
            new CheckBookingPaymentEligibilityQuery(command.BookingId),
            cancellationToken);

        if (eligibility.IsFailure)
        {
            return eligibility;
        }

        var ticketPrice = await bus.InvokeAsync<Result<decimal>>(
            new GetEventTicketPriceQuery(command.EventId),
            cancellationToken);

        if (ticketPrice.IsFailure)
        {
            return Result.Failure(ticketPrice.Error);
        }

        var outcome = paymentGateway.Process(command.UserId, command.Quantity);

        if (outcome.IsTechnicalFailure)
        {
            throw ToTechnicalFailure(outcome);
        }

        var processedAt = timeProvider.GetUtcNow();
        var paymentId = Guid.NewGuid();
        var amount = ticketPrice.Value * command.Quantity;
        var status = outcome.IsSuccess ? PaymentStatus.Succeeded : PaymentStatus.Failed;

        writeDb.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = command.BookingId,
            Amount = amount,
            Status = status,
            CreatedAt = processedAt,
            CompletedAt = processedAt
        });

        logger.LogInformation(
            "Payment {PaymentId} recorded as {PaymentStatus} for booking {BookingId} with amount {PaymentAmount}.",
            paymentId,
            status,
            command.BookingId,
            amount);

        if (outcome.IsSuccess)
        {
            await bus.PublishAsync(
                new PaymentSucceededEvent(
                    command.BookingId,
                    paymentId,
                    processedAt));

            return Result.Success();
        }

        await bus.PublishAsync(
            new PaymentFailedEvent(
                command.BookingId,
                paymentId,
                outcome.FailureReason ?? "Payment was declined.",
                processedAt));

        return Result.Success();
    }

    private static PaymentTechnicalFailureException ToTechnicalFailure(
        Domain.PaymentOutcomeDecision outcome)
    {
        var message = outcome.FailureReason ?? "The payment provider failed unexpectedly.";

        return outcome.RecoveryDecision switch
        {
            Domain.PaymentRecoveryDecisionKind.RuntimeManagedRecovery =>
                PaymentTechnicalFailureException.RuntimeManagedRecovery(message),
            Domain.PaymentRecoveryDecisionKind.EscalateOrManualIntervention =>
                PaymentTechnicalFailureException.EscalateOrManualIntervention(message),
            _ => outcome.ProviderState switch
            {
                Domain.PaymentProviderStateKind.DegradedRecoverable =>
                    PaymentTechnicalFailureException.DegradedRecoverable(message),
                Domain.PaymentProviderStateKind.Terminal =>
                    PaymentTechnicalFailureException.Terminal(message),
                _ => outcome.IsRetriableTechnicalFailure
                    ? PaymentTechnicalFailureException.Retriable(message)
                    : PaymentTechnicalFailureException.NonRetriable(message)
            }
        };
    }
}
