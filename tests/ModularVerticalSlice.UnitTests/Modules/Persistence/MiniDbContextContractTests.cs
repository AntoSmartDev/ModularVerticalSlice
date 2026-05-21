using System.Reflection;
using ModularVerticalSlice.Modules.Bookings.Persistence;
using ModularVerticalSlice.Modules.Catalog.Persistence;
using ModularVerticalSlice.Modules.Payments.Persistence;

namespace ModularVerticalSlice.UnitTests.Modules.Persistence;

/// <summary>
/// Verifies the baseline constraints applied to module mini DbContext contracts.
/// </summary>
public class MiniDbContextContractTests
{
    private static readonly Type[] ContractTypes =
    [
        typeof(ICatalogReadDbContext),
        typeof(ICatalogWriteDbContext),
        typeof(IBookingReadDbContext),
        typeof(IBookingWriteDbContext),
        typeof(IPaymentReadDbContext),
        typeof(IPaymentWriteDbContext)
    ];

    /// <summary>
    /// Verifies that mini DbContext contracts do not expose save semantics.
    /// </summary>
    [Fact]
    public void Mini_DbContext_Contracts_Should_Not_Expose_SaveChangesAsync()
    {
        Assert.All(
            ContractTypes,
            contract => Assert.DoesNotContain(
                contract.GetMethods(),
                method => method.Name == "SaveChangesAsync"));
    }

    /// <summary>
    /// Verifies that mini DbContext contracts do not expose repository-style methods.
    /// </summary>
    [Fact]
    public void Mini_DbContext_Contracts_Should_Not_Expose_Repository_Like_Methods()
    {
        var forbiddenPrefixes = new[] { "Get", "Find", "List", "Add", "Update", "Delete", "Remove" };

        Assert.All(
            ContractTypes,
            contract => Assert.DoesNotContain(
                contract.GetMethods(BindingFlags.Public | BindingFlags.Instance),
                method => forbiddenPrefixes.Any(prefix => method.Name.StartsWith(prefix, StringComparison.Ordinal))));
    }
}
