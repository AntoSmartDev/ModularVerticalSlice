using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;
using ModularVerticalSlice.Application.Modules.Payments.Persistence;
using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;

namespace ModularVerticalSlice.Persistence;

/// <summary>
/// Registers the concrete EF Core persistence services for the application.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Adds the application persistence baseline backed by PostgreSQL.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        // Explicit adapter types keep the DbContextSlice boundary narrow while still
        // making the concrete AppDbContext dependency visible to Wolverine 6.
        services.AddScoped<ICatalogReadDbContextSlice, CatalogReadDbContextAdapter>();
        services.AddScoped<ICatalogWriteDbContextSlice, CatalogWriteDbContextAdapter>();
        services.AddScoped<IBookingCatalogReadDbContextSlice, BookingCatalogReadDbContextAdapter>();
        services.AddScoped<IBookingReadDbContextSlice, BookingReadDbContextAdapter>();
        services.AddScoped<IBookingWriteDbContextSlice, BookingWriteDbContextAdapter>();
        services.AddScoped<IPaymentReadDbContextSlice, PaymentReadDbContextAdapter>();
        services.AddScoped<IPaymentWriteDbContextSlice, PaymentWriteDbContextAdapter>();

        return services;
    }
}

/// <summary>
/// Exposes the Catalog read-only slice over the shared application DbContext.
/// </summary>
/// <param name="db">The shared application DbContext.</param>
public sealed class CatalogReadDbContextAdapter(AppDbContext db) : ICatalogReadDbContextSlice
{
    /// <inheritdoc />
    public IQueryable<Event> Events => db.Events.AsNoTracking();
}

/// <summary>
/// Exposes the Catalog write slice over the shared application DbContext.
/// </summary>
/// <param name="db">The shared application DbContext.</param>
public sealed class CatalogWriteDbContextAdapter(AppDbContext db) : ICatalogWriteDbContextSlice
{
    /// <inheritdoc />
    public DbSet<Event> Events => db.Events;
}

/// <summary>
/// Exposes the composite Bookings/Catalog read slice over the shared application DbContext.
/// </summary>
/// <param name="db">The shared application DbContext.</param>
public sealed class BookingCatalogReadDbContextAdapter(AppDbContext db) : IBookingCatalogReadDbContextSlice
{
    /// <inheritdoc />
    public IQueryable<Booking> Bookings => db.Bookings.AsNoTracking();

    /// <inheritdoc />
    public IQueryable<Event> Events => db.Events.AsNoTracking();
}

/// <summary>
/// Exposes the Bookings read-only slice over the shared application DbContext.
/// </summary>
/// <param name="db">The shared application DbContext.</param>
public sealed class BookingReadDbContextAdapter(AppDbContext db) : IBookingReadDbContextSlice
{
    /// <inheritdoc />
    public IQueryable<Booking> Bookings => db.Bookings.AsNoTracking();
}

/// <summary>
/// Exposes the Bookings write slice over the shared application DbContext.
/// </summary>
/// <param name="db">The shared application DbContext.</param>
public sealed class BookingWriteDbContextAdapter(AppDbContext db) : IBookingWriteDbContextSlice
{
    /// <inheritdoc />
    public DbSet<Booking> Bookings => db.Bookings;
}

/// <summary>
/// Exposes the Payments read-only slice over the shared application DbContext.
/// </summary>
/// <param name="db">The shared application DbContext.</param>
public sealed class PaymentReadDbContextAdapter(AppDbContext db) : IPaymentReadDbContextSlice
{
    /// <inheritdoc />
    public IQueryable<Payment> Payments => db.Payments.AsNoTracking();
}

/// <summary>
/// Exposes the Payments write slice over the shared application DbContext.
/// </summary>
/// <param name="db">The shared application DbContext.</param>
public sealed class PaymentWriteDbContextAdapter(AppDbContext db) : IPaymentWriteDbContextSlice
{
    /// <inheritdoc />
    public DbSet<Payment> Payments => db.Payments;
}
