using System.Reflection;
using NetArchTest.Rules;
using ModularVerticalSlice.Application.Modules.Bookings;

namespace ModularVerticalSlice.ArchitectureTests;

public sealed class AppDbContextGuardrailTests
{
    private static readonly Assembly ApplicationAssembly = typeof(BookingsModule).Assembly;

    [Fact]
    public void Application_Does_Not_Depend_On_Persistence_Assembly()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn("ModularVerticalSlice.Persistence")
            .GetResult();

        Assert.True(result.IsSuccessful, FormatViolations(result));
    }

    private static string FormatViolations(NetArchTest.Rules.TestResult result) =>
        $"Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}";
}
