using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Bookings.Persistence;

/// <summary>
/// Write-side DbContextSlice for the Bookings module.
/// </summary>
/// <remarks>
/// A DbContextSlice is a narrow interface that exposes only the persistence surface
/// a single module needs. It is implemented by the shared <c>AppDbContext</c> — not
/// a separate DbContext instance — isolating the module from the full persistence graph.
/// </remarks>
public interface IBookingWriteDbContextSlice
{
    /// <summary>
    /// Gets the mutable bookings set owned by the module.
    /// </summary>
    DbSet<Booking> Bookings { get; }
}
