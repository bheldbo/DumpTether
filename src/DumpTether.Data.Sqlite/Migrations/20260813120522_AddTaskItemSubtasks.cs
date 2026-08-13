using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskItemSubtasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "parent_task_item_id",
                table: "task_items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_items_parent_task_item_id",
                table: "task_items",
                column: "parent_task_item_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_workspace_id_parent_task_item_id_archived_at_last_touched_at",
                table: "task_items",
                columns: new[] { "workspace_id", "parent_task_item_id", "archived_at", "last_touched_at" });

            migrationBuilder.AddForeignKey(
                name: "FK_task_items_task_items_parent_task_item_id",
                table: "task_items",
                column: "parent_task_item_id",
                principalTable: "task_items",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_items_task_items_parent_task_item_id",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_parent_task_item_id",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_workspace_id_parent_task_item_id_archived_at_last_touched_at",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "parent_task_item_id",
                table: "task_items");
        }
    }
}
