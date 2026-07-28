using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudSyncAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cloud_sync_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    cloud_api_base_url = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    cloud_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    cloud_email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    cloud_display_name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    protected_session_token = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    session_expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    connected_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_verified_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    disconnected_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cloud_sync_accounts", x => x.id);
                    table.ForeignKey(
                        name: "FK_cloud_sync_accounts_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_cloud_sync_accounts_user_id",
                table: "cloud_sync_accounts",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cloud_sync_accounts");
        }
    }
}
