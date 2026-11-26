using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PirryLedger.Core.Notifications.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDeliveryPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "event_key",
                table: "not_notifications",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "notification_id",
                table: "not_email_queue",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "notification_recipient_user_id",
                table: "not_email_queue",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "not_notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_not_notification_preferences", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_not_notifications_event_key_recipient_user_id",
                table: "not_notifications",
                columns: new[] { "event_key", "recipient_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_not_email_queue_notification_id",
                table: "not_email_queue",
                column: "notification_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_not_email_queue_not_notifications_notification_id",
                table: "not_email_queue",
                column: "notification_id",
                principalTable: "not_notifications",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_not_email_queue_not_notifications_notification_id",
                table: "not_email_queue");

            migrationBuilder.DropTable(
                name: "not_notification_preferences");

            migrationBuilder.DropIndex(
                name: "IX_not_notifications_event_key_recipient_user_id",
                table: "not_notifications");

            migrationBuilder.DropIndex(
                name: "IX_not_email_queue_notification_id",
                table: "not_email_queue");

            migrationBuilder.DropColumn(
                name: "event_key",
                table: "not_notifications");

            migrationBuilder.DropColumn(
                name: "notification_id",
                table: "not_email_queue");

            migrationBuilder.DropColumn(
                name: "notification_recipient_user_id",
                table: "not_email_queue");
        }
    }
}
