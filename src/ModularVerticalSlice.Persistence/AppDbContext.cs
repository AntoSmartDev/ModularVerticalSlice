using Microsoft.EntityFrameworkCore;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments.Persistence;
using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;

namespace ModularVerticalSlice.Persistence;

/// <summary>
/// Represents the concrete EF Core database context for the application.
/// </summary>
/// <remarks>
/// This context lives in the Persistence project so modules depend only on their
/// DbContextSlice interfaces rather than the full EF Core composition surface.
/// </remarks>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) :
    DbContext(options),
    IBookingCatalogReadDbContextSlice,
    IBookingReadDbContextSlice,
    IBookingWriteDbContextSlice,
    ICatalogReadDbContextSlice,
    ICatalogWriteDbContextSlice,
    IPaymentReadDbContextSlice,
    IPaymentWriteDbContextSlice
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
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EventConfiguration).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    IQueryable<Event> ICatalogReadDbContextSlice.Events => Set<Event>().AsNoTracking();

    IQueryable<Booking> IBookingReadDbContextSlice.Bookings => Set<Booking>().AsNoTracking();

    IQueryable<Booking> IBookingCatalogReadDbContextSlice.Bookings => Set<Booking>().AsNoTracking();

    IQueryable<Event> IBookingCatalogReadDbContextSlice.Events => Set<Event>().AsNoTracking();

    IQueryable<Payment> IPaymentReadDbContextSlice.Payments => Set<Payment>().AsNoTracking();
}
