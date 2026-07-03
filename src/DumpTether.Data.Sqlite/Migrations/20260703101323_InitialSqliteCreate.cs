using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class InitialSqliteCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    password_hash = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_login_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    email_confirmed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "workspaces",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_confirmation_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_email_confirmation_tokens", x => x.id);
                    table.ForeignKey(
                        name: "FK_email_confirmation_tokens_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_logins",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    provider = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    provider_user_id = table.Column<string>(type: "TEXT", maxLength: 256, nullable: false),
                    email_at_login = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_used_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_external_logins", x => x.id);
                    table.ForeignKey(
                        name: "FK_external_logins_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    header_layout_json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    entry_layout_json = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_templates", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_templates_app_users_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    session_token_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    session_type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    user_agent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    ip_address_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    device_name = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_sessions_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "archive_resolutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workspace_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    requires_explanation = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workspace_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    is_active = table.Column<bool>(type: "INTEGER", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                    table.ForeignKey(
                        name: "FK_projects_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sync_roots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    local_workspace_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    remote_workspace_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    cloud_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    device_id = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
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
                name: "workspace_invitations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workspace_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    invited_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    accepted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "workspace_memberships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workspace_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_memberships", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_memberships_app_users_user_id",
                        column: x => x.user_id,
                        principalTable: "app_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_workspace_memberships_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "field_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    task_template_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    key = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    label = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    type = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    scope = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false, defaultValue: "Header"),
                    is_required = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    options = table.Column<string>(type: "TEXT", nullable: true),
                    layout_row = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    layout_column = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    layout_row_span = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    layout_column_span = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    layout_weight = table.Column<double>(type: "REAL", nullable: false, defaultValue: 1.0),
                    deactivated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_definitions", x => x.id);
                    table.ForeignKey(
                        name: "FK_field_definitions_task_templates_task_template_id",
                        column: x => x.task_template_id,
                        principalTable: "task_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "saved_views",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workspace_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    project_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    scope = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    definition = table.Column<string>(type: "TEXT", nullable: false),
                    sort = table.Column<string>(type: "TEXT", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_views", x => x.id);
                    table.CheckConstraint("ck_saved_views_scope_project", "(scope = 'Workspace' AND project_id IS NULL) OR (scope = 'Project' AND project_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_saved_views_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_saved_views_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workspace_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    project_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    task_template_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    title = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    status = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    category = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    color = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_viewed_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    last_touched_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    follow_up_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    archive_resolution_id = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_items", x => x.id);
                    table.CheckConstraint("ck_task_items_archive_requires_resolution", "(archived_at IS NULL AND archive_resolution_id IS NULL) OR (archived_at IS NOT NULL AND archive_resolution_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_task_items_archive_resolutions_archive_resolution_id",
                        column: x => x.archive_resolution_id,
                        principalTable: "archive_resolutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_items_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_items_task_templates_task_template_id",
                        column: x => x.task_template_id,
                        principalTable: "task_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_items_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "sync_mappings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    sync_root_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    entity_type = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    local_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    remote_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    last_remote_version = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    last_synced_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
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

            migrationBuilder.CreateTable(
                name: "field_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    task_item_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    field_definition_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_field_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_field_values_field_definitions_field_definition_id",
                        column: x => x.field_definition_id,
                        principalTable: "field_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_field_values_task_items_task_item_id",
                        column: x => x.task_item_id,
                        principalTable: "task_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_item_shares",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    workspace_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    task_item_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    shared_with_user_id = table.Column<Guid>(type: "TEXT", nullable: true),
                    shared_by_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    role = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    token_hash = table.Column<string>(type: "TEXT", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    accepted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
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
                name: "task_timeline_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    task_item_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    kind = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    summary = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    details = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_timeline_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_timeline_entries_task_items_task_item_id",
                        column: x => x.task_item_id,
                        principalTable: "task_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_timeline_entry_field_values",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    task_timeline_entry_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    field_definition_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    value = table.Column<string>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_timeline_entry_field_values", x => x.id);
                    table.ForeignKey(
                        name: "FK_task_timeline_entry_field_values_field_definitions_field_definition_id",
                        column: x => x.field_definition_id,
                        principalTable: "field_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_task_timeline_entry_field_values_task_timeline_entries_task_timeline_entry_id",
                        column: x => x.task_timeline_entry_id,
                        principalTable: "task_timeline_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_users_normalized_email",
                table: "app_users",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_archive_resolutions_workspace_id_name",
                table: "archive_resolutions",
                columns: new[] { "workspace_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_confirmation_tokens_token_hash",
                table: "email_confirmation_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_email_confirmation_tokens_user_id_expires_at_used_at",
                table: "email_confirmation_tokens",
                columns: new[] { "user_id", "expires_at", "used_at" });

            migrationBuilder.CreateIndex(
                name: "IX_external_logins_provider_provider_user_id",
                table: "external_logins",
                columns: new[] { "provider", "provider_user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_external_logins_user_id",
                table: "external_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_field_definitions_task_template_id_scope_key",
                table: "field_definitions",
                columns: new[] { "task_template_id", "scope", "key" });

            migrationBuilder.CreateIndex(
                name: "IX_field_definitions_task_template_id_scope_sort_order",
                table: "field_definitions",
                columns: new[] { "task_template_id", "scope", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_field_values_field_definition_id",
                table: "field_values",
                column: "field_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_field_values_task_item_id_field_definition_id",
                table: "field_values",
                columns: new[] { "task_item_id", "field_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_workspace_id_name_is_active",
                table: "projects",
                columns: new[] { "workspace_id", "name", "is_active" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_saved_views_project_id",
                table: "saved_views",
                column: "project_id");

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
                name: "IX_task_item_shares_token_hash",
                table: "task_item_shares",
                column: "token_hash");

            migrationBuilder.CreateIndex(
                name: "IX_task_item_shares_workspace_id_normalized_email_revoked_at",
                table: "task_item_shares",
                columns: new[] { "workspace_id", "normalized_email", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_archive_resolution_id",
                table: "task_items",
                column: "archive_resolution_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_follow_up_at",
                table: "task_items",
                column: "follow_up_at");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_project_id",
                table: "task_items",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_task_template_id",
                table: "task_items",
                column: "task_template_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_items_workspace_id_archived_at_last_touched_at",
                table: "task_items",
                columns: new[] { "workspace_id", "archived_at", "last_touched_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_workspace_id_category",
                table: "task_items",
                columns: new[] { "workspace_id", "category" });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_workspace_id_color",
                table: "task_items",
                columns: new[] { "workspace_id", "color" });

            migrationBuilder.CreateIndex(
                name: "IX_task_items_workspace_id_project_id_archived_at",
                table: "task_items",
                columns: new[] { "workspace_id", "project_id", "archived_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_templates_owner_user_id_name",
                table: "task_templates",
                columns: new[] { "owner_user_id", "name" });

            migrationBuilder.CreateIndex(
                name: "IX_task_timeline_entries_task_item_id_occurred_at",
                table: "task_timeline_entries",
                columns: new[] { "task_item_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "IX_task_timeline_entry_field_values_field_definition_id",
                table: "task_timeline_entry_field_values",
                column: "field_definition_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_timeline_entry_field_values_task_timeline_entry_id_field_definition_id",
                table: "task_timeline_entry_field_values",
                columns: new[] { "task_timeline_entry_id", "field_definition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_session_token_hash",
                table: "user_sessions",
                column: "session_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_user_id_expires_at_revoked_at",
                table: "user_sessions",
                columns: new[] { "user_id", "expires_at", "revoked_at" });

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
                name: "IX_workspace_invitations_workspace_id_normalized_email_accepted_at_revoked_at",
                table: "workspace_invitations",
                columns: new[] { "workspace_id", "normalized_email", "accepted_at", "revoked_at" });

            migrationBuilder.CreateIndex(
                name: "IX_workspace_memberships_user_id",
                table: "workspace_memberships",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_memberships_workspace_id_user_id",
                table: "workspace_memberships",
                columns: new[] { "workspace_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspaces_name",
                table: "workspaces",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "email_confirmation_tokens");

            migrationBuilder.DropTable(
                name: "external_logins");

            migrationBuilder.DropTable(
                name: "field_values");

            migrationBuilder.DropTable(
                name: "saved_views");

            migrationBuilder.DropTable(
                name: "sync_mappings");

            migrationBuilder.DropTable(
                name: "task_item_shares");

            migrationBuilder.DropTable(
                name: "task_timeline_entry_field_values");

            migrationBuilder.DropTable(
                name: "user_sessions");

            migrationBuilder.DropTable(
                name: "workspace_invitations");

            migrationBuilder.DropTable(
                name: "workspace_memberships");

            migrationBuilder.DropTable(
                name: "sync_roots");

            migrationBuilder.DropTable(
                name: "field_definitions");

            migrationBuilder.DropTable(
                name: "task_timeline_entries");

            migrationBuilder.DropTable(
                name: "task_items");

            migrationBuilder.DropTable(
                name: "archive_resolutions");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "task_templates");

            migrationBuilder.DropTable(
                name: "workspaces");

            migrationBuilder.DropTable(
                name: "app_users");
        }
    }
}
