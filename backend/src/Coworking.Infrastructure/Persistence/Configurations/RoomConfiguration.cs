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

        // Room names are unique WITHOUT regard to letter case ("Office 1" = "office 1", CW-18).
        // A plain unique index on name compares case-sensitively, so it is NOT declared here.
        // Instead the migration RoomNameCaseInsensitiveUnique creates an EXPRESSION index:
        //   CREATE UNIQUE INDEX ux_rooms_name_lower ON rooms (lower(name));
        // PostgreSQL stores lower(name) in the index and rejects a second row whose lower-cased
        // name is already there (SqlState 23505 → 409 conflict.duplicate). EF Core can't describe
        // expression indexes in the model, so it is raw SQL in the migration – the same approach
        // as the BR01 EXCLUDE constraint. RoomService checks first and returns the friendlier
        // 409 rooms.name_taken; the index is the backstop for two requests at the same moment.

        // Prostorija 1 – 1..* RadnoMjesto. EF fills the read-only Workspaces property
        // through its backing field _workspaces (found by naming convention).
        builder
            .HasMany(r => r.Workspaces)
            .WithOne(w => w.Room)
            .HasForeignKey(w => w.RoomId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
