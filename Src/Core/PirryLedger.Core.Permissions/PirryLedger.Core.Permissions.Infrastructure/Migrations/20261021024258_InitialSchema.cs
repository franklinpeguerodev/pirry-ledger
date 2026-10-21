using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.Permissions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "per_permission_grants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    granted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_per_permission_grants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "per_permission_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_permission = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    applied_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejected_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_per_permission_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_per_permission_grants_permission",
                table: "per_permission_grants",
                column: "permission_name");

            migrationBuilder.CreateIndex(
                name: "ux_per_permission_grants_user_permission",
                table: "per_permission_grants",
                columns: new[] { "user_id", "permission_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_per_permission_requests_requester",
                table: "per_permission_requests",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "ix_per_permission_requests_state",
                table: "per_permission_requests",
                column: "state");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "per_permission_grants");

            migrationBuilder.DropTable(
                name: "per_permission_requests");
        }
    }
}
