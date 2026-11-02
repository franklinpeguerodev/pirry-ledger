using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Business.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBusinessSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bil_invoices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    issued_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_delivery = table.Column<bool>(type: "boolean", nullable: false),
                    debtor_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bil_invoice_edits",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    edited_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    edited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_invoice_edits", x => x.id);
                    table.ForeignKey(
                        name: "FK_bil_invoice_edits_bil_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "bil_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bil_invoice_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_invoice_items", x => x.id);
                    table.ForeignKey(
                        name: "FK_bil_invoice_items_bil_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "bil_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "bil_invoice_payments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    method = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    received_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    change_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    registered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_invoice_payments", x => x.id);
                    table.ForeignKey(
                        name: "FK_bil_invoice_payments_bil_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalTable: "bil_invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bil_invoice_edits_invoice_id",
                table: "bil_invoice_edits",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_bil_invoice_items_invoice_id",
                table: "bil_invoice_items",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "IX_bil_invoice_payments_invoice_id",
                table: "bil_invoice_payments",
                column: "invoice_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bil_invoice_edits");

            migrationBuilder.DropTable(
                name: "bil_invoice_items");

            migrationBuilder.DropTable(
                name: "bil_invoice_payments");

            migrationBuilder.DropTable(
                name: "bil_invoices");
        }
    }
}
