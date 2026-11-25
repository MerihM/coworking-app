using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>
/// Table memberships. CHECK constraints repeat the most important domain rules (BR05),
/// so even a bug or a manual SQL change can't create a "paid" membership without a period.
/// </summary>
internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable(
            "memberships",
            table =>
            {
                table.HasCheckConstraint("ck_memberships_price", "price >= 0");
                table.HasCheckConstraint("ck_memberships_duration", "duration_days > 0");

                // BR05: anything other than Unpaid must have a validity period.
                table.HasCheckConstraint(
                    "ck_memberships_paid_has_period",
                    "status = 'Unpaid' OR (start_date IS NOT NULL AND end_date IS NOT NULL)"
                );

                // EndDate is exclusive and always after StartDate (ADR 0002).
                table.HasCheckConstraint(
                    "ck_memberships_period",
                    "end_date IS NULL OR end_date > start_date"
                );
            }
        );

        // TipClanarine 1 – 0..* Clanarina (no collection on the type side: not needed).
        builder
            .HasOne(m => m.MembershipType)
            .WithMany()
            .HasForeignKey(m => m.MembershipTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Clanarina 1 – 0..1 Uplata. One-to-one creates a UNIQUE index on payments.membership_id:
        // the database guarantees at most one payment per membership.
        builder
            .HasOne(m => m.Payment)
            .WithOne(p => p.Membership)
            .HasForeignKey<Payment>(p => p.MembershipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
