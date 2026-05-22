using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskWallMetadataAndEditableNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "task_timeline_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "task_timeline_entries",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.Sql(
                "UPDATE task_timeline_entries SET updated_at = occurred_at;");

            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "task_items",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "task_items",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_items_workspace_id_category",
                table: "task_items",
                columns: new[] { "workspace_id", "category" });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_workspace_id_color",
                table: "task_items",
                columns: new[] { "workspace_id", "color" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_items_workspace_id_category",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_workspace_id_color",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "task_timeline_entries");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "task_timeline_entries");

            migrationBuilder.DropColumn(
                name: "category",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "color",
                table: "task_items");
        }
    }
}
