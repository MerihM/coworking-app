using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coworking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MembershipTypeNameCaseInsensitiveUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_membership_types_name", table: "membership_types");

            // Case-insensitive unique membership type names (CW-20), same approach as rooms (CW-18).
            migrationBuilder.Sql(
                "CREATE UNIQUE INDEX ux_membership_types_name_lower ON membership_types (lower(name));"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_membership_types_name_lower;");

            migrationBuilder.CreateIndex(
                name: "ix_membership_types_name",
                table: "membership_types",
                column: "name",
                unique: true
            );
        }
    }
}
