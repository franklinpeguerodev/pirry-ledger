using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable


namespace PirryLedger.Business.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "subcategory_id",
                table: "bil_products",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "bil_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bil_subcategories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_subcategories", x => x.id);
                    table.ForeignKey(
                        name: "FK_bil_subcategories_bil_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "bil_categories",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bil_products_subcategory_id",
                table: "bil_products",
                column: "subcategory_id");

            migrationBuilder.CreateIndex(
                name: "IX_bil_subcategories_category_id",
                table: "bil_subcategories",
                column: "category_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bil_products_bil_subcategories_subcategory_id",
                table: "bil_products",
                column: "subcategory_id",
                principalTable: "bil_subcategories",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bil_products_bil_subcategories_subcategory_id",
                table: "bil_products");

            migrationBuilder.DropTable(
                name: "bil_subcategories");

            migrationBuilder.DropTable(
                name: "bil_categories");

            migrationBuilder.DropIndex(
                name: "IX_bil_products_subcategory_id",
                table: "bil_products");

            migrationBuilder.DropColumn(
                name: "subcategory_id",
                table: "bil_products");
        }
    }
}
