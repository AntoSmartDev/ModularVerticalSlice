using System.Reflection;
using NetArchTest.Rules;
using ModularVerticalSlice.Application.Modules.Bookings;

namespace ModularVerticalSlice.ArchitectureTests;

/// <summary>
/// Modules must not reference each other's internal namespaces directly.
/// Cross-module communication is allowed only via the public Messages namespace.
/// </summary>
public sealed class ProjectBoundaryTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(BookingsModule).Assembly;

    [Fact]
    public void Bookings_Does_Not_Depend_On_Other_Module_Internals()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Modules.Bookings")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Catalog.Features",
                "ModularVerticalSlice.Application.Modules.Catalog.Domain",
                "ModularVerticalSlice.Application.Modules.Payments.Features",
                "ModularVerticalSlice.Application.Modules.Payments.Domain",
                "ModularVerticalSlice.Application.Modules.Notifications.Features",
                "ModularVerticalSlice.Application.Modules.Notifications.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Catalog_Does_Not_Depend_On_Other_Module_Internals()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Modules.Catalog")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Bookings.Features",
                "ModularVerticalSlice.Application.Modules.Bookings.Domain",
                "ModularVerticalSlice.Application.Modules.Payments.Features",
                "ModularVerticalSlice.Application.Modules.Payments.Domain",
                "ModularVerticalSlice.Application.Modules.Notifications.Features",
                "ModularVerticalSlice.Application.Modules.Notifications.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Payments_Does_Not_Depend_On_Other_Module_Internals()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Modules.Payments")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Bookings.Features",
                "ModularVerticalSlice.Application.Modules.Bookings.Domain",
                "ModularVerticalSlice.Application.Modules.Catalog.Features",
                "ModularVerticalSlice.Application.Modules.Catalog.Domain",
                "ModularVerticalSlice.Application.Modules.Notifications.Features",
                "ModularVerticalSlice.Application.Modules.Notifications.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Notifications_Does_Not_Depend_On_Other_Module_Internals()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Modules.Notifications")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Bookings.Features",
                "ModularVerticalSlice.Application.Modules.Bookings.Domain",
                "ModularVerticalSlice.Application.Modules.Catalog.Features",
                "ModularVerticalSlice.Application.Modules.Catalog.Domain",
                "ModularVerticalSlice.Application.Modules.Payments.Features",
                "ModularVerticalSlice.Application.Modules.Payments.Domain")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    private static string FormatViolations(NetArchTest.Rules.TestResult result) =>
        $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
