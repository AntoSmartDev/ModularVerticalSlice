using Wolverine;

namespace ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle;

/// <summary>
/// Holds the minimum durable state required to correlate and compensate a booking lifecycle.
/// </summary>
/// <remarks>
/// The saga identity is the booking identifier. Booking status and other domain state remain
/// owned by the Booking entity instead of being duplicated in this orchestration state.
/// </remarks>
public sealed class BookingLifecycleSaga : Saga
{
    /// <summary>
    /// Gets or sets the booking identifier used as the saga correlation identity.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the event identifier required when ticket release compensation is needed.
    /// </summary>
    public Guid EventId { get; set; }

    /// <summary>
    /// Gets or sets the ticket quantity required when ticket release compensation is needed.
    /// </summary>
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets when the booking lifecycle started.
    /// </summary>
    public DateTimeOffset StartedAt { get; set; }
}
