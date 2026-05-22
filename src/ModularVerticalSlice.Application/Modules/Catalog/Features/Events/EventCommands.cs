namespace ModularVerticalSlice.Modules.Catalog.Features.Events;

/// <summary>
/// Requests the creation of a new catalog event.
/// </summary>
/// <param name="Title">The display title of the event.</param>
/// <param name="Date">The scheduled date and time of the event.</param>
/// <param name="TicketPrice">The ticket price configured for the event.</param>
/// <param name="AvailableTickets">The initial number of tickets available for reservation.</param>
public sealed record CreateEventCommand(
    string Title,
    DateTimeOffset Date,
    decimal TicketPrice,
    int AvailableTickets);
