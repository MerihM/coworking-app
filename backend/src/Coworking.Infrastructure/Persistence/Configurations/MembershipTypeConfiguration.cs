using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>Table membership_types: the price list.</summary>
internal sealed class MembershipTypeConfiguration : IEntityTypeConfiguration<MembershipType>
{
    public void Configure(EntityTypeBuilder<MembershipType> builder)
    {
        builder.ToTable(
            "membership_types",
            table =>
            {
                table.HasCheckConstraint("ck_membership_types_duration", "duration_days > 0");
                table.HasCheckConstraint("ck_membership_types_price", "price >= 0");
            }
        );

        builder.Property(t => t.Name).HasMaxLength(100);

        // Names are unique WITHOUT regard to letter case ("Monthly" = "monthly", CW-20), same as
        // room names (CW-18). EF Core can't describe expression indexes, so there is no HasIndex
        // here: migration MembershipTypeNameCaseInsensitiveUnique creates it with raw SQL:
        //   CREATE UNIQUE INDEX ux_membership_types_name_lower ON membership_types (lower(name));
        // MembershipTypeService checks first (409 membership_types.name_taken); the index is the
        // backstop for two requests at the same moment (23505 → 409 conflict.duplicate).
    }
}
