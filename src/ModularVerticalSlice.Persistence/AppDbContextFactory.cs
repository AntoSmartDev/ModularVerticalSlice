using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ModularVerticalSlice.Persistence;

/// <summary>
/// Creates <see cref="AppDbContext"/> instances for EF Core design-time operations.
/// </summary>
/// <remarks>
/// The factory keeps migration generation independent from the WebApi host while
/// still using the same baseline connection string conventions of the solution.
/// </remarks>
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=modularverticalslice;Username=postgres;Password=postgres";

    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("MODULAR_VERTICAL_SLICE_DATABASE") ??
            DefaultConnectionString;

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
