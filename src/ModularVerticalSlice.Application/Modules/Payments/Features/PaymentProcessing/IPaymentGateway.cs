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
