using ModularVerticalSlice.Application.Modules.Payments.Domain;

namespace ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

/// <summary>
/// Represents the provider-facing seam used by the Payments module to decide
/// the baseline outcome of a payment request.
/// </summary>
public interface IPaymentGateway
{
    /// <summary>
    /// Processes the payment request through the current gateway seam.
    /// </summary>
    PaymentOutcomeDecision Process(string userId, int quantity);
}

/// <summary>
/// Deterministic gateway used until a real provider integration is introduced.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    /// <summary>
    /// Delegates the payment outcome to the deterministic policy so the
    /// current behavior stays stable.
    /// </summary>
    public PaymentOutcomeDecision Process(string userId, int quantity)
    {
        if (userId.Contains("technical-terminal", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentOutcomeDecision.NonRetriableTechnicalFailure(
                "Payment provider rejected the request as non-retriable.");
        }

        if (userId.Contains("technical-failure", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentOutcomeDecision.RetriableTechnicalFailure(
                "Payment provider is temporarily unavailable.");
        }

        return PaymentOutcomePolicy.Decide(userId, quantity);
    }
}
