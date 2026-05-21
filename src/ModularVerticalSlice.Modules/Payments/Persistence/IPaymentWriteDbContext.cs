namespace ModularVerticalSlice.Modules.Payments.Persistence;

/// <summary>
/// Defines the write-side persistence surface reserved for the Payments module.
/// </summary>
/// <remarks>
/// The contract is introduced before the module persistence types exist so the
/// architectural boundary is established early without exposing generic repository methods
/// or `SaveChangesAsync`. It will be enriched with `DbSet` surfaces when Payments entities
/// are introduced in later milestones.
/// </remarks>
public interface IPaymentWriteDbContext
{
}
