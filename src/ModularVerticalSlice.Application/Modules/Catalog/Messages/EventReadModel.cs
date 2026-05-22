namespace ModularVerticalSlice.Application.Modules.Catalog.Messages;

/// <summary>
/// Stable read contract exposed by the Catalog module to other modules.
/// </summary>
/// <param name="Id">The event identifier.</param>
/// <param name="Title">The display title of the event.</param>
/// <param name="Date">The scheduled date and time of the event.</param>
/// <param name="TicketPrice">The current ticket price for the event.</param>
public sealed record EventReadModel(
    Guid Id,
    string Title,
    DateTimeOffset Date,
    decimal TicketPrice);
