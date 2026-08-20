using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveArchiveResolutions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                PRAGMA foreign_keys = 0;
                BEGIN TRANSACTION;

                DROP INDEX "IX_task_items_archive_resolution_id";

                CREATE TABLE "ef_temp_task_items" (
                    "id" TEXT NOT NULL CONSTRAINT "PK_task_items" PRIMARY KEY,
                    "archived_at" TEXT NULL,
                    "category" TEXT NULL,
                    "color" TEXT NULL,
                    "created_at" TEXT NOT NULL,
                    "follow_up_at" TEXT NULL,
                    "last_touched_at" TEXT NOT NULL,
                    "last_viewed_at" TEXT NULL,
                    "parent_task_item_id" TEXT NULL,
                    "project_id" TEXT NULL,
                    "status" TEXT NULL,
                    "task_template_id" TEXT NULL,
                    "title" TEXT NOT NULL,
                    "workspace_id" TEXT NOT NULL,
                    CONSTRAINT "FK_task_items_projects_project_id" FOREIGN KEY ("project_id") REFERENCES "projects" ("id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_task_items_task_items_parent_task_item_id" FOREIGN KEY ("parent_task_item_id") REFERENCES "task_items" ("id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_task_items_task_templates_task_template_id" FOREIGN KEY ("task_template_id") REFERENCES "task_templates" ("id") ON DELETE RESTRICT,
                    CONSTRAINT "FK_task_items_workspaces_workspace_id" FOREIGN KEY ("workspace_id") REFERENCES "workspaces" ("id") ON DELETE RESTRICT
                );

                INSERT INTO "ef_temp_task_items" (
                    "id", "archived_at", "category", "color", "created_at", "follow_up_at",
                    "last_touched_at", "last_viewed_at", "parent_task_item_id", "project_id",
                    "status", "task_template_id", "title", "workspace_id")
                SELECT
                    "id", "archived_at", "category", "color", "created_at", "follow_up_at",
                    "last_touched_at", "last_viewed_at", "parent_task_item_id", "project_id",
                    "status", "task_template_id", "title", "workspace_id"
                FROM "task_items";

                DROP TABLE "task_items";
                ALTER TABLE "ef_temp_task_items" RENAME TO "task_items";
                DROP TABLE "archive_resolutions";

                CREATE INDEX "IX_task_items_follow_up_at" ON "task_items" ("follow_up_at");
                CREATE INDEX "IX_task_items_parent_task_item_id" ON "task_items" ("parent_task_item_id");
                CREATE INDEX "IX_task_items_project_id" ON "task_items" ("project_id");
                CREATE INDEX "IX_task_items_task_template_id" ON "task_items" ("task_template_id");
                CREATE INDEX "IX_task_items_workspace_id_archived_at_last_touched_at" ON "task_items" ("workspace_id", "archived_at", "last_touched_at");
                CREATE INDEX "IX_task_items_workspace_id_category" ON "task_items" ("workspace_id", "category");
                CREATE INDEX "IX_task_items_workspace_id_color" ON "task_items" ("workspace_id", "color");
                CREATE INDEX "IX_task_items_workspace_id_parent_task_item_id_archived_at_last_touched_at" ON "task_items" ("workspace_id", "parent_task_item_id", "archived_at", "last_touched_at");
                CREATE INDEX "IX_task_items_workspace_id_project_id_archived_at" ON "task_items" ("workspace_id", "project_id", "archived_at");

                COMMIT;
                PRAGMA foreign_keys = 1;
                """,
                suppressTransaction: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "archive_resolution_id",
                table: "task_items",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "archive_resolutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    requires_explanation = table.Column<bool>(type: "INTEGER", nullable: false),
                    workspace_id = table.Column<Guid>(type: "TEXT", nullable: false)
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
                    0,
                    1,
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
