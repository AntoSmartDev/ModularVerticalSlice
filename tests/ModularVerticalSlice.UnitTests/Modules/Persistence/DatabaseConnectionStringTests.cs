using Microsoft.Extensions.Configuration;
using ModularVerticalSlice.Persistence;

namespace ModularVerticalSlice.UnitTests.Modules.Persistence;

/// <summary>
/// Verifies the single explicit database connection-string contract.
/// </summary>
public sealed class DatabaseConnectionStringTests
{
    /// <summary>
    /// Verifies that runtime components resolve the configured Database connection string.
    /// </summary>
    [Fact]
    public void Required_Database_Connection_String_Should_Resolve_Configured_Value()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = "configured-database"
            })
            .Build();

        Assert.Equal("configured-database", configuration.GetRequiredDatabaseConnectionString());
    }

    /// <summary>
    /// Verifies that missing database configuration fails during startup instead of using a hidden fallback.
    /// </summary>
    [Fact]
    public void Required_Database_Connection_String_Should_Fail_When_Missing()
    {
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<InvalidOperationException>(
            configuration.GetRequiredDatabaseConnectionString);

        Assert.Contains("ConnectionStrings:Database", exception.Message);
    }
}
