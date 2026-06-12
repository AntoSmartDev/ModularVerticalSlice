using System.Reflection;
using NetArchTest.Rules;
using ModularVerticalSlice.Application.Modules.Bookings;

namespace ModularVerticalSlice.ArchitectureTests;

/// <summary>
/// Wolverine message handlers must live in Application.Modules and must not
/// depend on WebApi infrastructure types.
/// </summary>
public sealed class HandlerRuleTests
{
    private static readonly Assembly ApplicationAssembly =
        typeof(BookingsModule).Assembly;

    [Fact]
    public void Handler_Classes_Reside_In_Application_Modules()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("Handler")
            .Should().ResideInNamespace("ModularVerticalSlice.Application.Modules")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Saga_Classes_Reside_In_Application_Modules()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("Saga")
            .Should().ResideInNamespace("ModularVerticalSlice.Application.Modules")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Handler_Classes_Do_Not_Depend_On_WebApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("Handler")
            .ShouldNot().HaveDependencyOn("ModularVerticalSlice.WebApi")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Saga_Classes_Do_Not_Depend_On_WebApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .That().HaveNameEndingWith("Saga")
            .ShouldNot().HaveDependencyOn("ModularVerticalSlice.WebApi")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    private static string FormatViolations(NetArchTest.Rules.TestResult result) =>
        $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
