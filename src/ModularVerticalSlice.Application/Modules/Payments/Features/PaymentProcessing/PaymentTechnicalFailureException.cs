namespace ModularVerticalSlice.Application.Modules.Payments.Features.PaymentProcessing;

/// <summary>
/// Raised when the payment gateway reports a technical failure that should be
/// delegated to runtime retry semantics.
/// </summary>
public sealed class PaymentTechnicalFailureException(string message) : Exception(message);
