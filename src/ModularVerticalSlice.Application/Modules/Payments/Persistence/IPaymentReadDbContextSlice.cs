using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Payments.Persistence;

/// <summary>
/// Read-side DbContextSlice for the Payments module.
/// </summary>
/// <remarks>
/// A DbContextSlice is a narrow interface that exposes only the persistence surface
/// a single module needs. It is implemented by the shared <c>AppDbContext</c> — not
/// a separate DbContext instance — isolating the module from the full persistence graph.
/// </remarks>
public interface IPaymentReadDbContextSlice
{
    /// <summary>
    /// Gets the queryable payments surface for read operations.
    /// </summary>
    IQueryable<Payment> Payments { get; }
}
