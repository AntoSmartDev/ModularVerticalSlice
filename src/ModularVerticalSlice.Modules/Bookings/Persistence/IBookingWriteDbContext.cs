using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Modules.Bookings.Persistence.Entities;

namespace ModularVerticalSlice.Modules.Bookings.Persistence;

/// <summary>
/// Defines the write-side persistence surface reserved for the Bookings module.
/// </summary>
/// <remarks>
/// The contract is introduced before the module persistence types exist so the
/// architectural boundary is established early without exposing generic repository methods
/// or `SaveChangesAsync`. It will be enriched with `DbSet` surfaces when Bookings entities
/// are introduced in later milestones.
/// </remarks>
public interface IBookingWriteDbContext
{
    /// <summary>
    /// Gets the mutable bookings set owned by the module.
    /// </summary>
    DbSet<Booking> Bookings { get; }
}
