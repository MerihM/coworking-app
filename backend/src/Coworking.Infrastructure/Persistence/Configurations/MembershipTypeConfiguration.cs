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
        builder.HasIndex(t => t.Name).IsUnique();
    }
}
