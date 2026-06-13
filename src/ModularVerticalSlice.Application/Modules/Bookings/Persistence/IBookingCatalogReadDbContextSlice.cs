using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Bookings.Persistence;

/// <summary>
/// Read-composition DbContextSlice for Bookings queries that need both booking
/// and catalog data from the same store.
/// </summary>
/// <remarks>
/// This is an explicit same-store read-side compromise, not the default module
/// collaboration pattern. It keeps module-specific DbContextSlice contracts clean
/// while allowing a single query across module-owned tables on the shared store.
/// The trade-off is weaker read isolation, so this slice must stay limited to
/// query flows and must never be used as a write boundary.
/// </remarks>
public interface IBookingCatalogReadDbContextSlice
{
    /// <summary>
    /// Gets the queryable bookings surface used by composed Bookings read flows.
    /// </summary>
    IQueryable<Booking> Bookings { get; }

    /// <summary>
    /// Gets the queryable catalog events surface used by composed Bookings read flows.
    /// </summary>
    IQueryable<Event> Events { get; }
}
