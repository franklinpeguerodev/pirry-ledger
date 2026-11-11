using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.Documents.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialDocumentsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "doc_documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    branch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    uploaded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_doc_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_doc_documents_deleted_at",
                table: "doc_documents",
                column: "deleted_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_doc_documents_uploader",
                table: "doc_documents",
                columns: new[] { "uploaded_by", "uploaded_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_doc_documents_storage_key",
                table: "doc_documents",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "doc_documents");
        }
    }
}
