using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Modules.Bookings.Persistence;
using ModularVerticalSlice.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Modules.Catalog.Persistence;
using ModularVerticalSlice.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Modules.Payments.Persistence;
using ModularVerticalSlice.Modules.Payments.Persistence.Entities;

namespace ModularVerticalSlice.Persistence;

/// <summary>
/// Represents the concrete EF Core database context for the application.
/// </summary>
/// <remarks>
/// This context lives in the Persistence project so modules depend only on constrained
/// mini DbContext abstractions rather than the full EF Core composition surface.
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) :
    DbContext(options),
    IBookingReadDbContext,
    IBookingWriteDbContext,
    ICatalogReadDbContext,
    ICatalogWriteDbContext,
    IPaymentReadDbContext,
    IPaymentWriteDbContext
{
    /// <summary>
    /// Gets the mutable catalog events set.
    /// </summary>
    public DbSet<Event> Events => Set<Event>();

    /// <summary>
    /// Gets the mutable bookings set.
    /// </summary>
    public DbSet<Booking> Bookings => Set<Booking>();

    /// <summary>
    /// Gets the mutable payments set.
    /// </summary>
    public DbSet<Payment> Payments => Set<Payment>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventConfiguration).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    IQueryable<Event> ICatalogReadDbContext.Events => Set<Event>().AsNoTracking();

    IQueryable<Booking> IBookingReadDbContext.Bookings => Set<Booking>().AsNoTracking();

    IQueryable<Payment> IPaymentReadDbContext.Payments => Set<Payment>().AsNoTracking();
}
