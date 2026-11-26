using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Business.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchBusinessDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La migración no reconstruye jornadas de facturas anteriores.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM bil_invoices) THEN
                        RAISE EXCEPTION 'La migración de jornadas requiere revisar y limpiar las facturas anteriores.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "business_day_id",
                table: "bil_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "issued_by_user_name",
                table: "bil_invoices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "bil_business_days",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    opened_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_open = table.Column<bool>(type: "boolean", nullable: false),
                    last_closed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closing_sold = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    closing_cash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    closing_confirmed_transfers = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    closing_pending_transfers = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    closing_debt = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    closing_pending_cancellation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    closing_cancelled = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    closing_not_delivered = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_business_days", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bil_invoices_business_day_id",
                table: "bil_invoices",
                column: "business_day_id");

            migrationBuilder.CreateIndex(
                name: "ux_bil_business_days_open_branch",
                table: "bil_business_days",
                column: "branch_id",
                unique: true,
                filter: "is_open = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_bil_invoices_bil_business_days_business_day_id",
                table: "bil_invoices",
                column: "business_day_id",
                principalTable: "bil_business_days",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bil_invoices_bil_business_days_business_day_id",
                table: "bil_invoices");

            migrationBuilder.DropTable(
                name: "bil_business_days");

            migrationBuilder.DropIndex(
                name: "IX_bil_invoices_business_day_id",
                table: "bil_invoices");

            migrationBuilder.DropColumn(
                name: "business_day_id",
                table: "bil_invoices");

            migrationBuilder.DropColumn(
                name: "issued_by_user_name",
                table: "bil_invoices");
        }
    }
}
