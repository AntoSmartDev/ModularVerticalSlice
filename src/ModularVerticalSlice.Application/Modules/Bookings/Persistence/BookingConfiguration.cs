using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularVerticalSlice.Application.Modules.Bookings.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Bookings.Persistence;

/// <summary>
/// Configures the relational mapping for the Bookings persistence baseline.
/// </summary>
/// <remarks>
/// The Bookings module owns both the entity and its EF mapping. The concrete
/// AppDbContext composes this configuration from the Persistence project.
/// </remarks>
public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    /// <summary>
    /// Identifies the authoritative database constraint for booking idempotency.
    /// </summary>
    public const string IdempotencyConstraintName = "IX_bookings_UserId_ClientRequestId";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable(
            "bookings",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_bookings_quantity_positive",
                "\"Quantity\" > 0"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.EventId);

        builder.HasIndex(x => new { x.UserId, x.ClientRequestId })
            .HasDatabaseName(IdempotencyConstraintName)
            .IsUnique();

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.Quantity)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.UserId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.ClientRequestId)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();
    }
}
