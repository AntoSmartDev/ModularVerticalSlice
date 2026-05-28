using ModularVerticalSlice.Application.Modules.Payments.Domain;

namespace ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

/// <summary>
/// Deterministic baseline gateway used until a real provider integration is
/// introduced.
/// </summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    /// <summary>
    /// Delegates the baseline payment outcome to the deterministic policy so
    /// the first-release behavior stays stable.
    /// </summary>
    public PaymentOutcomeDecision Process(string userId, int quantity)
    {
        if (userId.Contains("technical-terminal", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentOutcomeDecision.TechnicalFailure(
                "Payment provider rejected the request as non-retriable.",
                isRetriableTechnicalFailure: false);
        }

        if (userId.Contains("technical-failure", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentOutcomeDecision.TechnicalFailure(
                "Payment provider is temporarily unavailable.");
        }

        return PaymentOutcomePolicy.Decide(userId, quantity);
    }
}
