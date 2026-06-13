namespace ModularVerticalSlice.Application.Modules.Catalog.Messages;

/// <summary>
/// Requests the current ticket price for a specific catalog event.
/// </summary>
/// <param name="EventId">The target event identifier.</param>
public sealed record GetEventTicketPriceQuery(Guid EventId);
