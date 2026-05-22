using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspaceProjectColors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "workspaces",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "projects",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "color",
                table: "projects");
        }
    }
}
