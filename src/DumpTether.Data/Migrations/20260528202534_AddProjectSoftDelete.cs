using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_projects_workspace_id_name",
                table: "projects");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "projects",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "IX_projects_workspace_id_name_is_active",
                table: "projects",
                columns: new[] { "workspace_id", "name", "is_active" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_projects_workspace_id_name_is_active",
                table: "projects");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "projects");

            migrationBuilder.CreateIndex(
                name: "IX_projects_workspace_id_name",
                table: "projects",
                columns: new[] { "workspace_id", "name" },
                unique: true);
        }
    }
}
