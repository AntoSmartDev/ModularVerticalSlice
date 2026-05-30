using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Bookings.Persistence;

/// <summary>
/// Defines a dedicated read-composition surface for Bookings queries that need
/// both booking and catalog data from the same store.
/// </summary>
/// <remarks>
/// This is an explicit same-store read-side compromise, not the default module
/// collaboration pattern. It keeps module-specific mini DbContext contracts
/// clean while allowing a single query across module-owned tables on the shared
/// store. The trade-off is weaker read isolation, so this contract must stay
/// limited to query slices and must never be used as a write boundary.
/// </remarks>
public interface IBookingCatalogReadDbContext
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
