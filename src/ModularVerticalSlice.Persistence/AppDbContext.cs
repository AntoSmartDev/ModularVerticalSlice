using Microsoft.EntityFrameworkCore;

namespace ModularVerticalSlice.Persistence;

/// <summary>
/// Represents the concrete EF Core database context for the application.
/// </summary>
/// <remarks>
/// This context lives in the Persistence project so modules depend only on constrained
/// mini DbContext abstractions rather than the full EF Core composition surface.
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
