namespace ModularVerticalSlice.Modules.Shared.Security;

/// <summary>
/// Provides access to the current user identity for module-level application flows.
/// </summary>
public interface ICurrentUserContext
{
    /// <summary>
    /// Gets the current authenticated user identifier.
    /// </summary>
    string UserId { get; }
}
