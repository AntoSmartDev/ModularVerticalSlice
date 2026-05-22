namespace ModularVerticalSlice.Modules.Bookings.Persistence.Entities;

/// <summary>
/// Represents a persisted booking owned by the Bookings module.
/// </summary>
/// <remarks>
/// The first release baseline keeps only the data required to identify the
/// booking, preserve idempotency and support the core booking lifecycle.
/// Higher-level business behavior remains outside the persistence model.
/// </remarks>
public sealed class Booking
{
    /// <summary>
    /// Gets or sets the booking identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the event identifier the booking refers to.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Gets or sets the number of reserved tickets.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the current booking status.
    /// </summary>
    public BookingStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the authenticated user identifier that owns the booking.
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client-side idempotency key used to prevent duplicates.
    /// </summary>
    public Guid ClientRequestId { get; set; }

    /// <summary>
    /// Gets or sets the creation timestamp of the booking.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>
/// Defines the lifecycle states of a persisted booking.
/// </summary>
public enum BookingStatus
{
    /// <summary>
    /// The booking has been created and is awaiting payment completion.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// The booking has been successfully confirmed.
    /// </summary>
    Confirmed = 2,

    /// <summary>
    /// The booking has been cancelled.
    /// </summary>
    Cancelled = 3,

    /// <summary>
    /// The payment window expired before confirmation.
    /// </summary>
    Expired = 4,
}
