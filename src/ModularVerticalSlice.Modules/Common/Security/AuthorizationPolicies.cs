namespace ModularVerticalSlice.Modules.Common.Security;

/// <summary>
/// Defines the stable authorization policy names referenced by application modules.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// Policy required to read booking data.
    /// </summary>
    public const string BookingsRead = "bookings.read";

    /// <summary>
    /// Policy required to create or mutate booking data.
    /// </summary>
    public const string BookingsWrite = "bookings.write";
}
