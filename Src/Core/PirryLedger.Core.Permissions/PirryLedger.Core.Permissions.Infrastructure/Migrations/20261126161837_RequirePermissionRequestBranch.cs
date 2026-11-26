using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.Permissions.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RequirePermissionRequestBranch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "branch_id",
                table: "per_permission_requests",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_permission_request_branch_nonempty",
                table: "per_permission_requests",
                sql: "branch_id <> '00000000-0000-0000-0000-000000000000'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_permission_request_branch_nonempty",
                table: "per_permission_requests");

            migrationBuilder.AlterColumn<Guid>(
                name: "branch_id",
                table: "per_permission_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
