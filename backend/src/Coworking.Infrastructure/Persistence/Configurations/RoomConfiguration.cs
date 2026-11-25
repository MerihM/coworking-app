using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>Table rooms and its relationship to workspaces.</summary>
internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");

        builder.Property(r => r.Name).HasMaxLength(100);
        builder.Property(r => r.Description).HasMaxLength(1000);

        builder.HasIndex(r => r.Name).IsUnique();

        // Prostorija 1 – 1..* RadnoMjesto. EF fills the read-only Workspaces property
        // through its backing field _workspaces (found by naming convention).
        builder
            .HasMany(r => r.Workspaces)
            .WithOne(w => w.Room)
            .HasForeignKey(w => w.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
