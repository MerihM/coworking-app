using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table reservations.
/// BR01 (no overlapping confirmed reservations for the same workspace) is enforced by the
/// EXCLUDE constraint "ex_reservations_no_overlap", added with raw SQL in the InitialCreate
/// migration, because EF Core has no fluent API for exclusion constraints.
/// </summary>
internal sealed class ReservationConfiguration : IEntityTypeConfiguration<Reservation>
{
    public void Configure(EntityTypeBuilder<Reservation> builder)
    {
        builder.ToTable(
            "reservations",
            table => table.HasCheckConstraint("ck_reservations_period", "ends_at > starts_at")
        );

        // RadnoMjesto 1 – 0..* Rezervacija (no collection on the workspace side).
        builder
            .HasOne(r => r.Workspace)
            .WithMany()
            .HasForeignKey(r => r.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional: the employee who cancelled on behalf of the member (change history).
        builder
            .HasOne(r => r.CancelledByEmployee)
            .WithMany()
            .HasForeignKey(r => r.CancelledByEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
