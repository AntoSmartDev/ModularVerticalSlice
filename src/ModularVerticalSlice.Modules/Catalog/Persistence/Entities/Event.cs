namespace ModularVerticalSlice.Modules.Catalog.Persistence.Entities;

/// <summary>
/// Represents a persisted catalog event available for booking flows.
/// </summary>
/// <remarks>
/// This is the module-owned persistence model for the first release baseline.
/// It stays intentionally simple and persistence-focused, without embedding
/// higher-level business behavior that belongs to handlers or domain policies.
/// </remarks>
public sealed class Event
{
    /// <summary>
    /// Gets or sets the event identifier.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the display title of the event.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the scheduled date and time of the event.
    /// </summary>
    public DateTimeOffset Date { get; set; }

    /// <summary>
    /// Gets or sets the ticket price for the event.
    /// </summary>
    public decimal TicketPrice { get; set; }

    /// <summary>
    /// Gets or sets the number of tickets still available for reservation.
    /// </summary>
    public int AvailableTickets { get; set; }
}
