using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.AccessControl.Infrastructure.Migrations;

public partial class AddBranchesAndUserBranch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ac_branches",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                address = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                is_all = table.Column<bool>(type: "boolean", nullable: false),
                active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ac_branches", x => x.id));

        migrationBuilder.InsertData(
            "ac_branches",
            new[] { "id", "name", "address", "is_all", "active" },
            new object[] { new Guid("00000000-0000-0000-0000-000000000001"), "Todas", "Todas las sucursales", true, true });

        migrationBuilder.AddColumn<Guid>(
            name: "branch_id",
            table: "ac_users",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE ac_users
            SET branch_id = '00000000-0000-0000-0000-000000000001'
            WHERE role = 'Administrator';
            DO $$
            BEGIN
                IF EXISTS (SELECT 1 FROM ac_users WHERE role = 'Standard' AND branch_id IS NULL) THEN
                    RAISE EXCEPTION 'No se puede migrar: existen usuarios Standard sin sucursal asignada.';
                END IF;
            END $$;
            """);

        migrationBuilder.AlterColumn<Guid>(
            name: "branch_id",
            table: "ac_users",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_ac_branches_name",
            table: "ac_branches",
            column: "name",
            unique: true);

        migrationBuilder.AddForeignKey(
            name: "FK_ac_users_ac_branches_branch_id",
            table: "ac_users",
            column: "branch_id",
            principalTable: "ac_branches",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_ac_users_ac_branches_branch_id", "ac_users");
        migrationBuilder.DropColumn("branch_id", "ac_users");
        migrationBuilder.DropTable("ac_branches");
    }
}
