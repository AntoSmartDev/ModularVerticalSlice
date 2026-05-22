using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Payments.Persistence;

/// <summary>
/// Defines the read-side persistence surface reserved for the Payments module.
/// </summary>
/// <remarks>
/// The contract is introduced before the module persistence types exist so the
/// architectural boundary is established early without inventing repository-like APIs.
/// It will be enriched with queryable surfaces when Payments entities and public
/// read models are introduced in later milestones.
/// </remarks>
public interface IPaymentReadDbContext
{
    /// <summary>
    /// Gets the queryable payments surface for read operations.
    /// </summary>
    IQueryable<Payment> Payments { get; }
}
