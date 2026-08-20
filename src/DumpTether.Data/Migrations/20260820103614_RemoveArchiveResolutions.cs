using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveArchiveResolutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_task_items_archive_resolutions_archive_resolution_id",
                table: "task_items");

            migrationBuilder.DropIndex(
                name: "IX_task_items_archive_resolution_id",
                table: "task_items");

            migrationBuilder.DropCheckConstraint(
                name: "ck_task_items_archive_requires_resolution",
                table: "task_items");

            migrationBuilder.DropColumn(
                name: "archive_resolution_id",
                table: "task_items");

            migrationBuilder.DropTable(
                name: "archive_resolutions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "archive_resolution_id",
                table: "task_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "archive_resolutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    requires_explanation = table.Column<bool>(type: "boolean", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_archive_resolutions", x => x.id);
                    table.ForeignKey(
                        name: "FK_archive_resolutions_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_archive_resolution_id",
                table: "task_items",
                column: "archive_resolution_id");

            migrationBuilder.Sql(
                """
                INSERT INTO archive_resolutions
                    (id, workspace_id, name, description, requires_explanation, is_active, created_at)
                SELECT
                    id,
                    id,
                    'Archived (rollback)',
                    'Generated while rolling back the direct archive migration.',
                    FALSE,
                    TRUE,
                    CURRENT_TIMESTAMP
                FROM workspaces;

                UPDATE task_items
                SET archive_resolution_id = workspace_id
                WHERE archived_at IS NOT NULL;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_task_items_archive_requires_resolution",
                table: "task_items",
                sql: "(archived_at IS NULL AND archive_resolution_id IS NULL) OR (archived_at IS NOT NULL AND archive_resolution_id IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_archive_resolutions_workspace_id_name",
                table: "archive_resolutions",
                columns: new[] { "workspace_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_task_items_archive_resolutions_archive_resolution_id",
                table: "task_items",
                column: "archive_resolution_id",
                principalTable: "archive_resolutions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
