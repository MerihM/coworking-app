using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>Table workspaces: unique code, capacity that fits the type.</summary>
internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable(
            "workspaces",
            table =>
            {
                table.HasCheckConstraint("ck_workspaces_capacity", "capacity > 0");

                // CW-19: the same ranges as Workspace.CapacityRange. Enums are stored as their
                // names, so the constraint compares strings. If a new WorkspaceType is ever added,
                // this constraint must be extended in the same migration – otherwise inserts fail.
                table.HasCheckConstraint(
                    "ck_workspaces_capacity_by_type",
                    "(type IN ('HotDesk', 'FixedDesk') AND capacity = 1)"
                        + " OR (type = 'Office' AND capacity BETWEEN 1 AND 20)"
                        + " OR (type = 'MeetingRoom' AND capacity BETWEEN 2 AND 50)"
                );
            }
        );

        builder.Property(w => w.Code).HasMaxLength(Workspace.CodeMaxLength);

        // "A1" must identify exactly one workspace in the whole center.
        builder.HasIndex(w => w.Code).IsUnique();
    }
}
