using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Business.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceIssuer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No atribuir facturas antiguas a un usuario ficticio.
            migrationBuilder.Sql("""
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM bil_invoices) THEN
                        RAISE EXCEPTION 'La migración de emisor requiere revisar y limpiar las facturas anteriores.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AddColumn<Guid>(
                name: "issued_by_user_id",
                table: "bil_invoices",
                type: "uuid",
                nullable: false);

            migrationBuilder.CreateIndex(
                name: "IX_bil_invoices_branch_id_issued_by_user_id",
                table: "bil_invoices",
                columns: new[] { "branch_id", "issued_by_user_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bil_invoices_branch_id_issued_by_user_id",
                table: "bil_invoices");

            migrationBuilder.DropColumn(
                name: "issued_by_user_id",
                table: "bil_invoices");
        }
    }
}
