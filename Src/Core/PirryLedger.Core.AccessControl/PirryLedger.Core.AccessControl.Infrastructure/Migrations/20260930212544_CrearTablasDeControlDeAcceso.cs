using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearTablasDeControlDeAcceso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ac_usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Correo = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    HashDeContrasena = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Rol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Activo = table.Column<bool>(type: "boolean", nullable: false),
                    FechaDeCreacionUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CredencialVersion = table.Column<int>(type: "integer", nullable: false),
                    IntentosFallidos = table.Column<int>(type: "integer", nullable: false),
                    BloqueoHastaUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ac_usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ac_codigos_recuperacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    HashDelCodigo = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmitidoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ac_codigos_recuperacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ac_codigos_recuperacion_ac_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "ac_usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ac_tokens_activacion",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uuid", nullable: false),
                    HashDelToken = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmitidoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiraUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsadoUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ac_tokens_activacion", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ac_tokens_activacion_ac_usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "ac_usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ac_codigos_recuperacion_hash",
                table: "ac_codigos_recuperacion",
                column: "HashDelCodigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ac_codigos_recuperacion_usuario",
                table: "ac_codigos_recuperacion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "ix_ac_tokens_activacion_hash",
                table: "ac_tokens_activacion",
                column: "HashDelToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ac_tokens_activacion_usuario",
                table: "ac_tokens_activacion",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "ix_ac_usuarios_correo",
                table: "ac_usuarios",
                column: "Correo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ac_codigos_recuperacion");

            migrationBuilder.DropTable(
                name: "ac_tokens_activacion");

            migrationBuilder.DropTable(
                name: "ac_usuarios");
        }
    }
}
