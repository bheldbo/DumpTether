using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountRecoveryAndDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "has_password_credential",
                table: "app_users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(
                "UPDATE app_users SET has_password_credential = FALSE " +
                "WHERE EXISTS (SELECT 1 FROM external_logins WHERE external_logins.user_id = app_users.id);");

            migrationBuilder.CreateTable(
                name: "account_deletion_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reminder_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    scheduled_for = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reminder_sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reminder_claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    state = table.Column<int>(type: "integer", nullable: false),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_deletion_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_account_deletion_requests_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "password_reset_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_password_reset_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_password_reset_tokens_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_state_reminder_due_at_reminder_se~",
                table: "account_deletion_requests",
                columns: new[] { "state", "reminder_due_at", "reminder_sent_at" });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_state_scheduled_for",
                table: "account_deletion_requests",
                columns: new[] { "state", "scheduled_for" });

            migrationBuilder.CreateIndex(
                name: "IX_account_deletion_requests_user_id",
                table: "account_deletion_requests",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_token_hash",
                table: "password_reset_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_password_reset_tokens_user_id_expires_at_used_at",
                table: "password_reset_tokens",
                columns: new[] { "user_id", "expires_at", "used_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_deletion_requests");

            migrationBuilder.DropTable(
                name: "password_reset_tokens");

            migrationBuilder.DropColumn(
                name: "has_password_credential",
                table: "app_users");
        }
    }
}
