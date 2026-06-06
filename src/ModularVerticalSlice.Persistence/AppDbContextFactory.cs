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
    /// <inheritdoc />
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Database") ??
            throw new InvalidOperationException(
                "Environment variable 'ConnectionStrings__Database' is required for EF Core design-time operations.");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new AppDbContext(optionsBuilder.Options);
    }
}
