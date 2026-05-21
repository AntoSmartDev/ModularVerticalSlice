namespace ModularVerticalSlice.Modules.Payments.Persistence;

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
}
