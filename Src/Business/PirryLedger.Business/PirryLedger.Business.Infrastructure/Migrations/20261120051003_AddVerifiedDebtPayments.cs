using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Business.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVerifiedDebtPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "rejection_reason",
                table: "bil_invoice_payments",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "resolved_at_utc",
                table: "bil_invoice_payments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "resolved_by_user_id",
                table: "bil_invoice_payments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ux_bil_pending_debt_transfer",
                table: "bil_invoice_payments",
                column: "invoice_id",
                unique: true,
                filter: "kind = 1 AND method = 1 AND status = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_bil_pending_debt_transfer",
                table: "bil_invoice_payments");

            migrationBuilder.DropColumn(
                name: "rejection_reason",
                table: "bil_invoice_payments");

            migrationBuilder.DropColumn(
                name: "resolved_at_utc",
                table: "bil_invoice_payments");

            migrationBuilder.DropColumn(
                name: "resolved_by_user_id",
                table: "bil_invoice_payments");
        }
    }
}
