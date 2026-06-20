using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class UserOwnedTaskTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_templates_workspaces_workspace_id",
                table: "task_templates");

            migrationBuilder.DropIndex(
                name: "IX_task_templates_workspace_id_name",
                table: "task_templates");

            migrationBuilder.DropColumn(
                name: "workspace_id",
                table: "task_templates");

            migrationBuilder.AddColumn<Guid>(
                name: "owner_user_id",
                table: "task_templates",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_owner_user_id_name",
                table: "task_templates",
                columns: new[] { "owner_user_id", "name" });

            migrationBuilder.AddForeignKey(
                name: "FK_task_templates_app_users_owner_user_id",
                table: "task_templates",
                column: "owner_user_id",
                principalTable: "app_users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_templates_app_users_owner_user_id",
                table: "task_templates");

            migrationBuilder.DropIndex(
                name: "IX_task_templates_owner_user_id_name",
                table: "task_templates");

            migrationBuilder.DropColumn(
                name: "owner_user_id",
                table: "task_templates");

            migrationBuilder.AddColumn<Guid>(
                name: "workspace_id",
                table: "task_templates",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_workspace_id_name",
                table: "task_templates",
                columns: new[] { "workspace_id", "name" });

            migrationBuilder.AddForeignKey(
                name: "FK_task_templates_workspaces_workspace_id",
                table: "task_templates",
                column: "workspace_id",
                principalTable: "workspaces",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
