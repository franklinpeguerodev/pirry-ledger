using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CrearTablaCorreosEnCola : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "not_correos_en_cola",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    destinatario = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    asunto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cuerpo = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<int>(type: "integer", nullable: false),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    fecha_creacion_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_envio_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ultimo_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_not_correos_en_cola", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_not_correos_en_cola_estado_fecha_creacion",
                table: "not_correos_en_cola",
                columns: new[] { "estado", "fecha_creacion_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "not_correos_en_cola");
        }
    }
}
