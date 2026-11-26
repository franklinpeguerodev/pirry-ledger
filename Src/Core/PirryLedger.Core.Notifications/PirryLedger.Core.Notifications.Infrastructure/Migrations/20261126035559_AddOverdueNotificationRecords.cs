using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOverdueNotificationRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "not_overdue_notification_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    recorded_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_not_overdue_notification_records", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_not_overdue_notification_records_event_key",
                table: "not_overdue_notification_records",
                column: "event_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "not_overdue_notification_records");
        }
    }
}
