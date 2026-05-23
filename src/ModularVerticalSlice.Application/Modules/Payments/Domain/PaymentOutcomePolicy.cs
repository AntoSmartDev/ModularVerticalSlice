namespace ModularVerticalSlice.Application.Modules.Payments.Domain;

/// <summary>
/// Determines the baseline business outcome of a payment request.
/// </summary>
/// <remarks>
/// The first release keeps payment processing intentionally simple and
/// deterministic. Technical failures remain outside this policy and belong to
/// Wolverine retry, circuit breaker and DLQ behaviors. This policy decides only
/// the business outcome that the Payments module should publish.
/// </remarks>
public static class PaymentOutcomePolicy
{
    /// <summary>
    /// Evaluates the baseline business outcome of a payment request.
    /// </summary>
    /// <param name="userId">The authenticated user that owns the payment.</param>
    /// <param name="quantity">The number of tickets covered by the payment.</param>
    /// <returns>
    /// A deterministic business outcome for the current baseline. A user
    /// identifier containing <c>declined</c> forces a business decline so the
    /// failure path can be exercised predictably in tests.
    /// </returns>
    public static PaymentOutcomeDecision Decide(string userId, int quantity)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return PaymentOutcomeDecision.Fail("Missing payment owner.");
        }

        if (quantity <= 0)
        {
            return PaymentOutcomeDecision.Fail("Invalid payment quantity.");
        }

        if (userId.Contains("declined", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentOutcomeDecision.Fail("Payment was declined.");
        }

        return PaymentOutcomeDecision.Success();
    }
}

/// <summary>
/// Represents the business outcome chosen for a payment request.
/// </summary>
public sealed record PaymentOutcomeDecision(
    bool IsSuccess,
    string? FailureReason)
{
    /// <summary>
    /// Creates a successful payment outcome.
    /// </summary>
    public static PaymentOutcomeDecision Success() => new(true, null);

    /// <summary>
    /// Creates a failed payment outcome with a business reason.
    /// </summary>
    public static PaymentOutcomeDecision Fail(string failureReason) =>
        new(false, failureReason);
}
