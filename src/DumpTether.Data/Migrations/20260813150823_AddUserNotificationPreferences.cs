using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserNotificationPreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_notification_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sharing_activity_email_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    daily_summary_email_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    follow_up_reminder_email_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    daily_summary_claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_daily_summary_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    follow_up_reminder_claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_follow_up_reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_notification_preferences", x => x.user_id);
                    table.ForeignKey(
                        name: "FK_user_notification_preferences_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_notification_preferences");
        }
    }
}
