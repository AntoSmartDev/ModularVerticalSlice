using ModularVerticalSlice.SharedKernel;

namespace ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;

/// <summary>
/// Represents a persisted catalog event available for booking flows.
/// </summary>
/// <remarks>
/// This is the module-owned persistence model for the first release baseline.
/// It remains physically under persistence, but it can still own natural
/// business behavior related to ticket availability.
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

    /// <summary>
    /// Gets or sets the PostgreSQL transaction row version used for optimistic concurrency.
    /// </summary>
    public uint Version { get; set; }

    /// <summary>
    /// Reserves the requested quantity of tickets from the current availability.
    /// </summary>
    public Result ReserveTickets(int requestedQuantity)
    {
        if (requestedQuantity <= 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidQuantity",
                    "The requested ticket quantity must be greater than zero."));
        }

        if (AvailableTickets < 0)
        {
            return Result.Failure(
                Error.Validation(
                    "Catalog.InvalidAvailability",
                    "The available ticket count cannot be negative."));
        }

        if (requestedQuantity > AvailableTickets)
        {
            return Result.Failure(
                Error.Conflict(
                    "Catalog.NotEnoughTickets",
                    "Not enough tickets are available for the requested reservation."));
        }

        AvailableTickets -= requestedQuantity;

        return Result.Success();
    }

    /// <summary>
    /// Releases the requested quantity of tickets back to the current availability.
    /// </summary>
    public Result ReleaseTickets(int quantity)
    {
        AvailableTickets += quantity;
        return Result.Success();
    }
}
