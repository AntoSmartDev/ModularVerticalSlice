namespace ModularVerticalSlice.Modules.Catalog.Features.Events;

/// <summary>
/// Requests the list of upcoming catalog events.
/// </summary>
public sealed record GetUpcomingEventsQuery;

/// <summary>
/// Requests the details of a specific catalog event.
/// </summary>
/// <param name="EventId">The event identifier.</param>
public sealed record GetEventDetailsQuery(Guid EventId);
