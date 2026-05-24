using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModularVerticalSlice.Application.Modules.Catalog.Persistence.Entities;

namespace ModularVerticalSlice.Application.Modules.Catalog.Persistence;

/// <summary>
/// Configures the relational mapping for the Catalog event entity.
/// </summary>
/// <remarks>
/// The configuration lives in the Catalog module because the module owns
/// the event persistence model. The concrete AppDbContext composes this
/// configuration from the Persistence project.
/// </remarks>
public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable(
            "events",
            tableBuilder => tableBuilder.HasCheckConstraint(
                "ck_events_available_tickets_non_negative",
                "\"AvailableTickets\" >= 0"));

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Date)
            .IsRequired();

        builder.Property(x => x.TicketPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.AvailableTickets)
            .IsRequired();

    }
}
