using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Catalog.Persistence;

/// <summary>
/// Read-side DbContextSlice for the Catalog module.
/// </summary>
/// <remarks>
/// A DbContextSlice is a narrow interface that exposes only the persistence surface
/// a single module needs. It is implemented by the shared <c>AppDbContext</c> — not
/// a separate DbContext instance — isolating the module from the full persistence graph.
/// </remarks>
public interface ICatalogReadDbContextSlice
{
    /// <summary>
    /// Gets the queryable catalog events surface for read operations.
    /// </summary>
    IQueryable<Event> Events { get; }
}
