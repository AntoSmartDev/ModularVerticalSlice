using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Catalog.Persistence;

/// <summary>
/// Write-side DbContextSlice for the Catalog module.
/// </summary>
/// <remarks>
/// A DbContextSlice is a narrow interface that exposes only the persistence surface
/// a single module needs. It is implemented by the shared <c>AppDbContext</c> — not
/// a separate DbContext instance — isolating the module from the full persistence graph.
/// </remarks>
public interface ICatalogWriteDbContextSlice
{
    /// <summary>
    /// Gets the mutable catalog events set owned by the module.
    /// </summary>
    DbSet<Event> Events { get; }
}
