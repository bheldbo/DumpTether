using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskTemplatesAndFieldOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_templates_workspace_id_name",
                table: "task_templates");

            migrationBuilder.DropIndex(
                name: "IX_field_definitions_task_template_id_key",
                table: "field_definitions");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                table: "task_templates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "task_templates",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE task_templates SET updated_at = created_at WHERE updated_at IS NULL");

            migrationBuilder.AlterColumn<DateTimeOffset>(
                name: "updated_at",
                table: "task_templates",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTimeOffset),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deactivated_at",
                table: "field_definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "options",
                table: "field_definitions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_workspace_id_name",
                table: "task_templates",
                columns: new[] { "workspace_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_field_definitions_task_template_id_key",
                table: "field_definitions",
                columns: new[] { "task_template_id", "key" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_templates_workspace_id_name",
                table: "task_templates");

            migrationBuilder.DropIndex(
                name: "IX_field_definitions_task_template_id_key",
                table: "field_definitions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "task_templates");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "task_templates");

            migrationBuilder.DropColumn(
                name: "deactivated_at",
                table: "field_definitions");

            migrationBuilder.DropColumn(
                name: "options",
                table: "field_definitions");

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_workspace_id_name",
                table: "task_templates",
                columns: new[] { "workspace_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_field_definitions_task_template_id_key",
                table: "field_definitions",
                columns: new[] { "task_template_id", "key" },
                unique: true);
        }
    }
}
