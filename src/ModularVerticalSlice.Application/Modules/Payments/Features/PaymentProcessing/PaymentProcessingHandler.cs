using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Payments.Messages;
using ModularVerticalSlice.Application.Modules.Payments.Persistence;
using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;
using ModularVerticalSlice.SharedKernel;
using Wolverine;

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
    IPaymentWriteDbContext writeDb,
    ICatalogReadDbContext catalogReadDb,
    IPaymentGateway paymentGateway,
    IMessageBus bus,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Handles the baseline ProcessPayment command.
    /// </summary>
    public async Task<Result> Handle(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        var validation = PaymentProcessingValidators.Validate(command);
        if (validation.IsFailure)
        {
            return validation;
        }

        var ticketPrice = await catalogReadDb.Events
            .Where(x => x.Id == command.EventId)
            .Select(x => (decimal?)x.TicketPrice)
            .SingleOrDefaultAsync(cancellationToken);

        if (ticketPrice is null)
        {
            return Result.Failure(
                Error.NotFound(
                    "Catalog.EventNotFound",
                    "The target event was not found for payment processing."));
        }

        var outcome = paymentGateway.Process(command.UserId, command.Quantity);

        if (outcome.IsTechnicalFailure)
        {
            throw new PaymentTechnicalFailureException(
                outcome.FailureReason ?? "The payment provider failed unexpectedly.",
                outcome.IsRetriableTechnicalFailure);
        }

        var processedAt = timeProvider.GetUtcNow();
        var paymentId = Guid.NewGuid();

        writeDb.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = command.BookingId,
            Amount = ticketPrice.Value * command.Quantity,
            Status = outcome.IsSuccess ? PaymentStatus.Succeeded : PaymentStatus.Failed,
            CreatedAt = processedAt,
            CompletedAt = processedAt
        });

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
}
