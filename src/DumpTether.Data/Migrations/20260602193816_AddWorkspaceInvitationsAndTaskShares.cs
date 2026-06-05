using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceInvitationsAndTaskShares : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_item_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    shared_with_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    shared_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_item_shares", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_item_shares_app_users_shared_by_user_id",
                        column: x => x.shared_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_item_shares_app_users_shared_with_user_id",
                        column: x => x.shared_with_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_task_item_shares_task_items_task_item_id",
                        column: x => x.task_item_id,
                        principalTable: "task_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_task_item_shares_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "workspace_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_invitations", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_invitations_app_users_invited_by_user_id",
                        column: x => x.invited_by_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workspace_invitations_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_item_shares_shared_by_user_id",
                table: "task_item_shares",
                column: "shared_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_item_shares_shared_with_user_id",
                table: "task_item_shares",
                column: "shared_with_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_item_shares_task_item_id_normalized_email_revoked_at",
                table: "task_item_shares",
                columns: new[] { "task_item_id", "normalized_email", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_item_shares_workspace_id_normalized_email_revoked_at",
                table: "task_item_shares",
                columns: new[] { "workspace_id", "normalized_email", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_invited_by_user_id",
                table: "workspace_invitations",
                column: "invited_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_token_hash",
                table: "workspace_invitations",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_invitations_workspace_id_normalized_email_accepte~",
                table: "workspace_invitations",
                columns: new[] { "workspace_id", "normalized_email", "accepted_at", "revoked_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_item_shares");

            migrationBuilder.DropTable(
                name: "workspace_invitations");
        }
    }
}
