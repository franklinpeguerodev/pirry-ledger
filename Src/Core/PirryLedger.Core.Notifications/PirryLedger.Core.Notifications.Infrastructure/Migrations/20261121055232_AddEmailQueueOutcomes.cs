using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailQueueOutcomes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // La cola anterior no limitaba intentos. No volver a reclamar los agotados.
            migrationBuilder.Sql("UPDATE not_email_queue SET status = 4, last_error = 'Se registró un error de entrega.' WHERE status = 0 AND attempts >= 5");
            migrationBuilder.AddColumn<DateTime>(
                name: "last_attempt_at_utc",
                table: "not_email_queue",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "status_changed_at_utc",
                table: "not_email_queue",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_attempt_at_utc",
                table: "not_email_queue");

            migrationBuilder.DropColumn(
                name: "status_changed_at_utc",
                table: "not_email_queue");
        }
    }
}
