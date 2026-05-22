namespace ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;

/// <summary>
/// Represents a persisted payment attempt owned by the Payments module.
/// </summary>
/// <remarks>
/// The first release baseline keeps only the data required to correlate the
/// payment with a booking and track the high-level payment outcome over time.
/// Provider-specific details and richer audit fields can be introduced later.
/// </remarks>
public sealed class Payment
{
    /// <summary>
    /// Gets or sets the payment identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the booking identifier this payment belongs to.
    /// </summary>
    public Guid BookingId { get; set; }

    /// <summary>
    /// Gets or sets the total amount requested for the payment.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the current payment status.
    /// </summary>
    public PaymentStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the payment entry was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the payment reached a terminal outcome.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }
}

/// <summary>
/// Defines the high-level lifecycle states of a persisted payment.
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// The payment request has been created and is still pending.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// The payment completed successfully.
    /// </summary>
    Succeeded = 2,

    /// <summary>
    /// The payment completed as a business failure.
    /// </summary>
    Failed = 3,
}
