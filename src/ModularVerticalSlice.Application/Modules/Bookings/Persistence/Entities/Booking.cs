using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;

/// <summary>
/// Represents a persisted booking owned by the Bookings module.
/// </summary>
/// <remarks>
/// The first release baseline keeps the entity physically under persistence,
/// but the entity can still own the natural transitions of the booking lifecycle.
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

    /// <summary>
    /// Confirms the booking after successful payment completion.
    /// </summary>
    public Result Confirm()
    {
        if (Status != BookingStatus.Pending)
        {
            return Result.Failure(
                Error.Conflict(
                    "Bookings.InvalidConfirmation",
                    "Only pending bookings can be confirmed."));
        }

        Status = BookingStatus.Confirmed;
        return Result.Success();
    }

    /// <summary>
    /// Cancels the booking after a business failure or explicit decision.
    /// </summary>
    public Result Cancel()
    {
        if (Status != BookingStatus.Pending)
        {
            return Result.Failure(
                Error.Conflict(
                    "Bookings.InvalidCancellation",
                    "Only pending bookings can be cancelled."));
        }

        Status = BookingStatus.Cancelled;
        return Result.Success();
    }

    /// <summary>
    /// Expires the booking after the payment window elapses.
    /// </summary>
    public Result Expire()
    {
        if (Status != BookingStatus.Pending)
        {
            return Result.Failure(
                Error.Conflict(
                    "Bookings.InvalidExpiration",
                    "Only pending bookings can expire."));
        }

        Status = BookingStatus.Expired;
        return Result.Success();
    }
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
