using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Business.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "image_revision",
                table: "bil_products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "bil_image_revision",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_image_revision", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bil_product_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    image_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    version = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    size_bytes = table.Column<int>(type: "integer", nullable: false),
                    revision_added = table.Column<long>(type: "bigint", nullable: false),
                    revision_removed = table.Column<long>(type: "bigint", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    removed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bil_product_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_bil_product_images_bil_products_product_id",
                        column: x => x.product_id,
                        principalTable: "bil_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bil_product_images_revision_added",
                table: "bil_product_images",
                column: "revision_added");

            migrationBuilder.CreateIndex(
                name: "ix_bil_product_images_revision_removed",
                table: "bil_product_images",
                column: "revision_removed");

            migrationBuilder.CreateIndex(
                name: "ux_bil_product_images_vigente",
                table: "bil_product_images",
                column: "product_id",
                unique: true,
                filter: "revision_removed IS NULL");

            // La fila del contador global de revisiones (ADR 015). Tiene que
            // existir antes de la primera subida: el repositorio la bloquea
            // para reservar la siguiente revision.
            migrationBuilder.InsertData(
                table: "bil_image_revision",
                columns: new[] { "id", "value" },
                values: new object[] { 1, 0L });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bil_image_revision");

            migrationBuilder.DropTable(
                name: "bil_product_images");

            migrationBuilder.DropColumn(
                name: "image_revision",
                table: "bil_products");
        }
    }
}
