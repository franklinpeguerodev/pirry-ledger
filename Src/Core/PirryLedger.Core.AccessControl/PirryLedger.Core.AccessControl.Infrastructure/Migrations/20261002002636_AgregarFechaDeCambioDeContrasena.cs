using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.AccessControl.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AgregarFechaDeCambioDeContrasena : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ContrasenaCambiadaUtc",
                table: "ac_usuarios",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContrasenaCambiadaUtc",
                table: "ac_usuarios");
        }
    }
}
