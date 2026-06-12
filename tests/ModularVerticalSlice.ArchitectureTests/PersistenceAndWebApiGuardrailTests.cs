using System.Reflection;
using NetArchTest.Rules;
using ModularVerticalSlice.Application.Modules.Bookings;
using ModularVerticalSlice.WebApi;

namespace ModularVerticalSlice.ArchitectureTests;

public sealed class PersistenceAndWebApiGuardrailTests
{
    private static readonly Assembly ApplicationAssembly = typeof(BookingsModule).Assembly;
    private static readonly Assembly WebApiAssembly = typeof(Program).Assembly;

    [Fact]
    public void WebApi_Does_Not_Reference_Persistence_Entity_Namespaces()
    {
        var result = Types.InAssembly(WebApiAssembly)
            .ShouldNot().HaveDependencyOnAny(
                "ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities",
                "ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities",
                "ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    [Fact]
    public void Application_Modules_Do_Not_Depend_On_WebApi()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("ModularVerticalSlice.WebApi")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    private static string FormatViolations(NetArchTest.Rules.TestResult result) =>
        $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
