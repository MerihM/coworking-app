using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Coworking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RoomNameCaseInsensitiveUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_rooms_name", table: "rooms");

            // Case-insensitive unique room names (CW-18). EF can't model expression indexes,
            // so it is raw SQL – same approach as the BR01 EXCLUDE constraint.
            migrationBuilder.Sql("CREATE UNIQUE INDEX ux_rooms_name_lower ON rooms (lower(name));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // IF EXISTS: Down must also work on a database where Up was applied before the
            // expression index was added (happened in dev during CW-18).
            migrationBuilder.Sql("DROP INDEX IF EXISTS ux_rooms_name_lower;");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_name",
                table: "rooms",
                column: "name",
                unique: true
            );
        }
    }
}
