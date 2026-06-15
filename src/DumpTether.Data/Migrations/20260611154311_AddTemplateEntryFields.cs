using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateEntryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_field_definitions_task_template_id_key",
                table: "field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_field_definitions_task_template_id_sort_order",
                table: "field_definitions");

            migrationBuilder.AddColumn<string>(
                name: "scope",
                table: "field_definitions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Header");

            migrationBuilder.AddColumn<int>(
                name: "layout_row",
                table: "field_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "layout_column",
                table: "field_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "layout_row_span",
                table: "field_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "layout_column_span",
                table: "field_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "task_timeline_entry_field_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_timeline_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_timeline_entry_field_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_timeline_entry_field_values_field_definitions_field_de~",
                        column: x => x.field_definition_id,
                        principalTable: "field_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_timeline_entry_field_values_task_timeline_entries_task~",
                        column: x => x.task_timeline_entry_id,
                        principalTable: "task_timeline_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_field_definitions_task_template_id_scope_key",
                table: "field_definitions",
                columns: new[] { "task_template_id", "scope", "key" });

            migrationBuilder.CreateIndex(
                name: "IX_field_definitions_task_template_id_scope_sort_order",
                table: "field_definitions",
                columns: new[] { "task_template_id", "scope", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_task_timeline_entry_field_values_field_definition_id",
                table: "task_timeline_entry_field_values",
                column: "field_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_timeline_entry_field_values_task_timeline_entry_id_fie~",
                table: "task_timeline_entry_field_values",
                columns: new[] { "task_timeline_entry_id", "field_definition_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "task_timeline_entry_field_values");

            migrationBuilder.DropIndex(
                name: "IX_field_definitions_task_template_id_scope_key",
                table: "field_definitions");

            migrationBuilder.DropIndex(
                name: "IX_field_definitions_task_template_id_scope_sort_order",
                table: "field_definitions");

            migrationBuilder.DropColumn(
                name: "scope",
                table: "field_definitions");

            migrationBuilder.DropColumn(
                name: "layout_row",
                table: "field_definitions");

            migrationBuilder.DropColumn(
                name: "layout_column",
                table: "field_definitions");

            migrationBuilder.DropColumn(
                name: "layout_row_span",
                table: "field_definitions");

            migrationBuilder.DropColumn(
                name: "layout_column_span",
                table: "field_definitions");

            migrationBuilder.CreateIndex(
                name: "IX_field_definitions_task_template_id_key",
                table: "field_definitions",
                columns: new[] { "task_template_id", "key" });

            migrationBuilder.CreateIndex(
                name: "IX_field_definitions_task_template_id_sort_order",
                table: "field_definitions",
                columns: new[] { "task_template_id", "sort_order" });
        }
    }
}
