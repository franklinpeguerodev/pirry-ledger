using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Business.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "product_id",
                table: "bil_invoice_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bil_products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_products", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bil_invoice_items_product_id",
                table: "bil_invoice_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "IX_bil_products_name",
                table: "bil_products",
                column: "name",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_bil_invoice_items_bil_products_product_id",
                table: "bil_invoice_items",
                column: "product_id",
                principalTable: "bil_products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bil_invoice_items_bil_products_product_id",
                table: "bil_invoice_items");

            migrationBuilder.DropTable(
                name: "bil_products");

            migrationBuilder.DropIndex(
                name: "IX_bil_invoice_items_product_id",
                table: "bil_invoice_items");

            migrationBuilder.DropColumn(
                name: "product_id",
                table: "bil_invoice_items");
        }
    }
}
