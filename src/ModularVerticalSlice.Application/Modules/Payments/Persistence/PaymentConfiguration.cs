using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularVerticalSlice.Application.Modules.Payments.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Payments.Persistence;

/// <summary>
/// Configures the relational mapping for the Payments persistence baseline.
/// </summary>
/// <remarks>
/// The Payments module owns both the payment entity and its EF mapping. The
/// concrete AppDbContext composes this configuration from the Persistence project.
/// </remarks>
public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable(
            "payments",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_payments_amount_positive",
                "amount > 0"));

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.BookingId)
            .IsUnique();

        builder.Property(x => x.BookingId)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.CompletedAt);
    }
}
