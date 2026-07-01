using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "sync_roots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    local_workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    remote_workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cloud_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    device_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_roots", x => x.id);
                    table.ForeignKey(
                        name: "FK_sync_roots_workspaces_local_workspace_id",
                        column: x => x.local_workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sync_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sync_root_id = table.Column<Guid>(type: "uuid", nullable: false),
                    entity_type = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    local_id = table.Column<Guid>(type: "uuid", nullable: false),
                    remote_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_remote_version = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sync_mappings", x => x.id);
                    table.ForeignKey(
                        name: "FK_sync_mappings_sync_roots_sync_root_id",
                        column: x => x.sync_root_id,
                        principalTable: "sync_roots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_sync_mappings_sync_root_id_entity_type_local_id",
                table: "sync_mappings",
                columns: new[] { "sync_root_id", "entity_type", "local_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sync_mappings_sync_root_id_entity_type_remote_id",
                table: "sync_mappings",
                columns: new[] { "sync_root_id", "entity_type", "remote_id" },
                unique: true,
                filter: "remote_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sync_roots_cloud_user_id_remote_workspace_id",
                table: "sync_roots",
                columns: new[] { "cloud_user_id", "remote_workspace_id" },
                unique: true,
                filter: "cloud_user_id IS NOT NULL AND remote_workspace_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_sync_roots_local_workspace_id",
                table: "sync_roots",
                column: "local_workspace_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "sync_mappings");

            migrationBuilder.DropTable(
                name: "sync_roots");
        }
    }
}
