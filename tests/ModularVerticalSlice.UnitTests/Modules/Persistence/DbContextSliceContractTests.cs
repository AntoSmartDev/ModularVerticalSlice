using System.Reflection;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Payments.Persistence;

namespace ModularVerticalSlice.UnitTests.Modules.Persistence;

/// <summary>
/// Verifies the baseline constraints applied to module DbContextSlice contracts.
/// </summary>
public class DbContextSliceContractTests
{
    private static readonly Type[] ContractTypes =
    [
        typeof(ICatalogReadDbContextSlice),
        typeof(ICatalogWriteDbContextSlice),
        typeof(IBookingReadDbContextSlice),
        typeof(IBookingWriteDbContextSlice),
        typeof(IPaymentReadDbContextSlice),
        typeof(IPaymentWriteDbContextSlice)
    ];

    /// <summary>
    /// Verifies that DbContextSlice contracts do not expose save semantics.
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
    /// Verifies that DbContextSlice contracts do not expose repository-style methods.
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
