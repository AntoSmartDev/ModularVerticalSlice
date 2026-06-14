using System.Reflection;
using NetArchTest.Rules;
using ModularVerticalSlice.Application.Modules.Bookings;

namespace ModularVerticalSlice.ArchitectureTests;

/// <summary>
/// Modules must not reference each other's internal namespaces directly.
/// Cross-module communication is allowed only via the public Messages and
/// Contracts namespaces. Same-store read-composition exceptions remain explicit
/// and local to dedicated Persistence slices, so Persistence is guarded on
/// feature/runtime code rather than blocked indiscriminately at module scope.
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
                "ModularVerticalSlice.Application.Delivery.BookingConfirmation")
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
                "ModularVerticalSlice.Application.Delivery.BookingConfirmation")
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
                "ModularVerticalSlice.Application.Delivery.BookingConfirmation")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void BookingConfirmation_Delivery_Does_Not_Depend_On_Module_Internals()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Delivery.BookingConfirmation")
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

    [Fact]
    public void Bookings_CreateBooking_Does_Not_Depend_On_Other_Module_Persistence()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Modules.Bookings.Features.CreateBooking")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence",
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Bookings_BookingLifecycle_Does_Not_Depend_On_Other_Module_Persistence()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Modules.Bookings.Features.BookingLifecycle")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence",
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Catalog_Features_Do_Not_Depend_On_Other_Module_Persistence()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Modules.Catalog.Features")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Bookings.Persistence",
                "ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Payments_Features_Do_Not_Depend_On_Other_Module_Persistence()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Modules.Payments.Features")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Bookings.Persistence",
                "ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities",
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence",
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void BookingConfirmation_Delivery_Does_Not_Depend_On_Module_Persistence()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().ResideInNamespace("ModularVerticalSlice.Application.Delivery.BookingConfirmation")
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Bookings.Persistence",
                "ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities",
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence",
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    private static string FormatViolations(NetArchTest.Rules.TestResult result) =>
        $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
