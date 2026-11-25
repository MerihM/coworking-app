using Coworking.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Coworking.Infrastructure.Persistence.Configurations;

/// <summary>Table workspaces: unique code, capacity at least 1.</summary>
internal sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable(
            "workspaces",
            table => table.HasCheckConstraint("ck_workspaces_capacity", "capacity > 0")
        );

        builder.Property(w => w.Code).HasMaxLength(20);

        // "A1" must identify exactly one workspace in the whole center.
        builder.HasIndex(w => w.Code).IsUnique();
    }
}
