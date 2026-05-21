using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedViewsAndStaleFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_saved_views_workspace_id_name",
                table: "saved_views");

            migrationBuilder.DropIndex(
                name: "IX_saved_views_workspace_id_project_id_name",
                table: "saved_views");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "saved_views",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "sort",
                table: "saved_views",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{\"field\":\"lastTouchedAt\",\"direction\":\"desc\"}'::jsonb");

            migrationBuilder.AddColumn<int>(
                name: "sort_order",
                table: "saved_views",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "saved_views",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "NOW()");

            migrationBuilder.CreateIndex(
                name: "IX_saved_views_workspace_id_name",
                table: "saved_views",
                columns: new[] { "workspace_id", "name" },
                unique: true,
                filter: "project_id IS NULL AND deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_views_workspace_id_project_id_name",
                table: "saved_views",
                columns: new[] { "workspace_id", "project_id", "name" },
                unique: true,
                filter: "project_id IS NOT NULL AND deleted_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_saved_views_workspace_id_name",
                table: "saved_views");

            migrationBuilder.DropIndex(
                name: "IX_saved_views_workspace_id_project_id_name",
                table: "saved_views");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "saved_views");

            migrationBuilder.DropColumn(
                name: "sort",
                table: "saved_views");

            migrationBuilder.DropColumn(
                name: "sort_order",
                table: "saved_views");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "saved_views");

            migrationBuilder.CreateIndex(
                name: "IX_saved_views_workspace_id_name",
                table: "saved_views",
                columns: new[] { "workspace_id", "name" },
                unique: true,
                filter: "project_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_saved_views_workspace_id_project_id_name",
                table: "saved_views",
                columns: new[] { "workspace_id", "project_id", "name" },
                unique: true,
                filter: "project_id IS NOT NULL");
        }
    }
}
