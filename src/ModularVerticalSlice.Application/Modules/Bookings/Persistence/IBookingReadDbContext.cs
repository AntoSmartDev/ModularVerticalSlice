using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Bookings.Persistence;

/// <summary>
/// Defines the read-side persistence surface reserved for the Bookings module.
/// </summary>
/// <remarks>
/// The contract is introduced before the module persistence types exist so the
/// architectural boundary is established early without inventing repository-like APIs.
/// It will be enriched with queryable surfaces when Bookings entities and public
/// read models are introduced in later milestones.
/// </remarks>
public interface IBookingReadDbContext
{
    /// <summary>
    /// Gets the queryable bookings surface for read operations.
    /// </summary>
    IQueryable<Booking> Bookings { get; }
}
