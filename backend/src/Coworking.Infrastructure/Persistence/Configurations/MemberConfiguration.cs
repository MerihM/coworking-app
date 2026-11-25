using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>Table members: personal data, unique e-mail, fixed desk, memberships and reservations.</summary>
internal sealed class MemberConfiguration : IEntityTypeConfiguration<Member>
{
    public void Configure(EntityTypeBuilder<Member> builder)
    {
        builder.ToTable("members");

        builder.Property(m => m.FirstName).HasMaxLength(100);
        builder.Property(m => m.LastName).HasMaxLength(100);
        builder.Property(m => m.Email).HasMaxLength(256);
        builder.Property(m => m.Phone).HasMaxLength(30);
        builder.Property(m => m.PasswordHash).HasMaxLength(256);

        // E-mail is the login name: unique. The entity already stores it in lower case,
        // so a plain unique index is enough for case-insensitive uniqueness.
        builder.HasIndex(m => m.Email).IsUnique();

        // Clan – RadnoMjesto (fixed desk) 0..1 – 0..1. A one-to-one relationship creates a UNIQUE
        // index on fixed_workspace_id, so one desk can't be assigned to two members.
        // If the workspace is deleted, the assignment is simply removed (SET NULL).
        builder
            .HasOne(m => m.FixedWorkspace)
            .WithOne()
            .HasForeignKey<Member>(m => m.FixedWorkspaceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Clan 1 – 0..* Clanarina. Restrict: a member with memberships can't be deleted (history).
        builder
            .HasMany(m => m.Memberships)
            .WithOne(ms => ms.Member)
            .HasForeignKey(ms => ms.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        // Clan 1 – 0..* Rezervacija.
        builder
            .HasMany(m => m.Reservations)
            .WithOne(r => r.Member)
            .HasForeignKey(r => r.MemberId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
