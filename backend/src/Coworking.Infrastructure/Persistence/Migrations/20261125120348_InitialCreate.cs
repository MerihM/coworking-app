using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Coworking.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder
                .AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:btree_gist", ",,");

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    first_name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    last_name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    email = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    password_hash = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    role = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employees", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "membership_types",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(
                        type: "numeric(10,2)",
                        precision: 10,
                        scale: 2,
                        nullable: false
                    ),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_membership_types", x => x.id);
                    table.CheckConstraint("ck_membership_types_duration", "duration_days > 0");
                    table.CheckConstraint("ck_membership_types_price", "price >= 0");
                }
            );

            migrationBuilder.CreateTable(
                name: "rooms",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    floor = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(
                        type: "character varying(1000)",
                        maxLength: 1000,
                        nullable: true
                    ),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rooms", x => x.id);
                }
            );

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    code = table.Column<string>(
                        type: "character varying(20)",
                        maxLength: 20,
                        nullable: false
                    ),
                    type = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    room_id = table.Column<int>(type: "integer", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspaces", x => x.id);
                    table.CheckConstraint("ck_workspaces_capacity", "capacity > 0");
                    table.ForeignKey(
                        name: "fk_workspaces_rooms_room_id",
                        column: x => x.room_id,
                        principalTable: "rooms",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    first_name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    last_name = table.Column<string>(
                        type: "character varying(100)",
                        maxLength: 100,
                        nullable: false
                    ),
                    email = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    phone = table.Column<string>(
                        type: "character varying(30)",
                        maxLength: 30,
                        nullable: false
                    ),
                    password_hash = table.Column<string>(
                        type: "character varying(256)",
                        maxLength: 256,
                        nullable: false
                    ),
                    registered_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    fixed_workspace_id = table.Column<int>(type: "integer", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_members_workspaces_fixed_workspace_id",
                        column: x => x.fixed_workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "memberships",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    membership_type_id = table.Column<int>(type: "integer", nullable: false),
                    purchased_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    price = table.Column<decimal>(
                        type: "numeric(10,2)",
                        precision: 10,
                        scale: 2,
                        nullable: false
                    ),
                    duration_days = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_memberships", x => x.id);
                    table.CheckConstraint("ck_memberships_duration", "duration_days > 0");
                    table.CheckConstraint(
                        "ck_memberships_paid_has_period",
                        "status = 'Unpaid' OR (start_date IS NOT NULL AND end_date IS NOT NULL)"
                    );
                    table.CheckConstraint(
                        "ck_memberships_period",
                        "end_date IS NULL OR end_date > start_date"
                    );
                    table.CheckConstraint("ck_memberships_price", "price >= 0");
                    table.ForeignKey(
                        name: "fk_memberships_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_memberships_membership_types_membership_type_id",
                        column: x => x.membership_type_id,
                        principalTable: "membership_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "reservations",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    member_id = table.Column<int>(type: "integer", nullable: false),
                    workspace_id = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    ends_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    created_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    status = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                    cancelled_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: true
                    ),
                    cancelled_by_employee_id = table.Column<int>(type: "integer", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_reservations", x => x.id);
                    table.CheckConstraint("ck_reservations_period", "ends_at > starts_at");
                    table.ForeignKey(
                        name: "fk_reservations_employees_cancelled_by_employee_id",
                        column: x => x.cancelled_by_employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_reservations_members_member_id",
                        column: x => x.member_id,
                        principalTable: "members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_reservations_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "payments",
                columns: table => new
                {
                    id = table
                        .Column<int>(type: "integer", nullable: false)
                        .Annotation(
                            "Npgsql:ValueGenerationStrategy",
                            NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                        ),
                    membership_id = table.Column<int>(type: "integer", nullable: false),
                    employee_id = table.Column<int>(type: "integer", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                    amount = table.Column<decimal>(
                        type: "numeric(10,2)",
                        precision: 10,
                        scale: 2,
                        nullable: false
                    ),
                    method = table.Column<string>(
                        type: "character varying(32)",
                        maxLength: 32,
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_payments", x => x.id);
                    table.CheckConstraint("ck_payments_amount", "amount >= 0");
                    table.ForeignKey(
                        name: "fk_payments_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "fk_payments_memberships_membership_id",
                        column: x => x.membership_id,
                        principalTable: "memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "ix_employees_email",
                table: "employees",
                column: "email",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_members_email",
                table: "members",
                column: "email",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_members_fixed_workspace_id",
                table: "members",
                column: "fixed_workspace_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_membership_types_name",
                table: "membership_types",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_memberships_member_id",
                table: "memberships",
                column: "member_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_memberships_membership_type_id",
                table: "memberships",
                column: "membership_type_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_payments_employee_id",
                table: "payments",
                column: "employee_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_payments_membership_id",
                table: "payments",
                column: "membership_id",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_reservations_cancelled_by_employee_id",
                table: "reservations",
                column: "cancelled_by_employee_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_reservations_member_id",
                table: "reservations",
                column: "member_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_reservations_workspace_id",
                table: "reservations",
                column: "workspace_id"
            );

            migrationBuilder.CreateIndex(
                name: "ix_rooms_name",
                table: "rooms",
                column: "name",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_code",
                table: "workspaces",
                column: "code",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "ix_workspaces_room_id",
                table: "workspaces",
                column: "room_id"
            );
            // ---------------------------------------------------------------
            // BR01: no two CONFIRMED reservations of the same workspace may overlap.
            // Added by hand: EF Core has no fluent API for exclusion constraints.
            //   workspace_id WITH =                        -> same workspace...
            //   tstzrange(starts_at, ends_at, '[)') WITH && -> ...and overlapping periods
            //   WHERE (status = 'Confirmed')               -> cancelled/completed don't block
            // '[)' = half-open range: 10:00-12:00 and 12:00-14:00 do NOT overlap (TC04).
            // Unlike a check in code, this also holds when two requests arrive at the same time.
            // Violation = PostgreSQL error 23P01 (exclusion_violation).
            // ---------------------------------------------------------------
            migrationBuilder.Sql(
                """
                ALTER TABLE reservations
                    ADD CONSTRAINT ex_reservations_no_overlap
                    EXCLUDE USING gist (
                        workspace_id WITH =,
                        tstzrange(starts_at, ends_at, '[)') WITH &&
                    )
                    WHERE (status = 'Confirmed');
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "ALTER TABLE reservations DROP CONSTRAINT IF EXISTS ex_reservations_no_overlap;"
            );

            migrationBuilder.DropTable(name: "payments");

            migrationBuilder.DropTable(name: "reservations");

            migrationBuilder.DropTable(name: "memberships");

            migrationBuilder.DropTable(name: "employees");

            migrationBuilder.DropTable(name: "members");

            migrationBuilder.DropTable(name: "membership_types");

            migrationBuilder.DropTable(name: "workspaces");

            migrationBuilder.DropTable(name: "rooms");
        }
    }
}
