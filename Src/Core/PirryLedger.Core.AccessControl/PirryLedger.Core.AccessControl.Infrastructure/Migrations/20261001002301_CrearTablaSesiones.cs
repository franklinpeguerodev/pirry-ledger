using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearTablaSesiones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ac_sesiones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    HashDelToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmitidaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CerradaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CredencialVersion = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ac_sesiones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ac_sesiones_ac_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "ac_usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ac_sesiones_expira",
                table: "ac_sesiones",
                column: "ExpiraUtc");

            migrationBuilder.CreateIndex(
                name: "ix_ac_sesiones_hash",
                table: "ac_sesiones",
                column: "HashDelToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ac_sesiones_usuario",
                table: "ac_sesiones",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ac_sesiones");
        }
    }
}
