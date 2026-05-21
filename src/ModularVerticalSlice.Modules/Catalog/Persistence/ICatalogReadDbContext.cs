namespace ModularVerticalSlice.Modules.Catalog.Persistence;

/// <summary>
/// Defines the read-side persistence surface reserved for the Catalog module.
/// </summary>
/// <remarks>
/// The contract is introduced before the module persistence types exist so the
/// architectural boundary is established early without inventing repository-like APIs.
/// It will be enriched with queryable surfaces when Catalog entities and read models
/// are introduced in later milestones.
/// </remarks>
public interface ICatalogReadDbContext
{
}
