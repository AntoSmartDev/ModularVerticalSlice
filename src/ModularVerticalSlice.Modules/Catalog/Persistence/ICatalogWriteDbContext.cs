namespace ModularVerticalSlice.Modules.Catalog.Persistence;

/// <summary>
/// Defines the write-side persistence surface reserved for the Catalog module.
/// </summary>
/// <remarks>
/// The contract is introduced before the module persistence types exist so the
/// architectural boundary is established early without exposing generic repository methods
/// or `SaveChangesAsync`. It will be enriched with `DbSet` surfaces when Catalog entities
/// are introduced in later milestones.
/// </remarks>
public interface ICatalogWriteDbContext
{
}
