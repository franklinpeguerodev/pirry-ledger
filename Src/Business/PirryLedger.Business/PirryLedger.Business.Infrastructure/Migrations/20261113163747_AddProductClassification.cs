using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

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

            migrationBuilder.InsertData(
                table: "bil_categories",
                columns: new[] { "id", "name" },
                values: new object[,]
                {
                    { new Guid("ca7e0000-0000-0000-0000-000000000001"), "Comidas" },
                    { new Guid("ca7e0000-0000-0000-0000-000000000002"), "Bebidas" }
                });

            migrationBuilder.InsertData(
                table: "bil_subcategories",
                columns: new[] { "id", "category_id", "name" },
                values: new object[,]
                {
                    { new Guid("ca7e0000-0000-0000-0001-000000000001"), new Guid("ca7e0000-0000-0000-0000-000000000001"), "Chimis" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000002"), new Guid("ca7e0000-0000-0000-0000-000000000001"), "Club sándwiches" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000003"), new Guid("ca7e0000-0000-0000-0000-000000000001"), "Sándwiches" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000004"), new Guid("ca7e0000-0000-0000-0000-000000000001"), "Yaroas" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000005"), new Guid("ca7e0000-0000-0000-0000-000000000001"), "Hamburguesas" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000006"), new Guid("ca7e0000-0000-0000-0000-000000000001"), "Tostones" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000007"), new Guid("ca7e0000-0000-0000-0000-000000000002"), "Jugos naturales" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000008"), new Guid("ca7e0000-0000-0000-0000-000000000002"), "Batidas" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000009"), new Guid("ca7e0000-0000-0000-0000-000000000002"), "Refrescos" },
                    { new Guid("ca7e0000-0000-0000-0001-000000000010"), new Guid("ca7e0000-0000-0000-0000-000000000002"), "Agua" }
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
