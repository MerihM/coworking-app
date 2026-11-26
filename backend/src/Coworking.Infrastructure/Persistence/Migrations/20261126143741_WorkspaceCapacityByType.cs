using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coworking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WorkspaceCapacityByType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_workspaces_capacity_by_type",
                table: "workspaces",
                sql: "(type IN ('HotDesk', 'FixedDesk') AND capacity = 1) OR (type = 'Office' AND capacity BETWEEN 1 AND 20) OR (type = 'MeetingRoom' AND capacity BETWEEN 2 AND 50)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_workspaces_capacity_by_type",
                table: "workspaces");
        }
    }
}
