using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCloudWorkspaceCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "updated_at",
                table: "workspaces",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.Sql(
                "UPDATE workspaces SET updated_at = created_at;");

            migrationBuilder.AddColumn<string>(
                name: "origin",
                table: "sync_roots",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "LocalEnrolled");

            migrationBuilder.AddColumn<string>(
                name: "remote_access_kind",
                table: "sync_roots",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "remote_role",
                table: "sync_roots",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "workspaces");

            migrationBuilder.DropColumn(
                name: "origin",
                table: "sync_roots");

            migrationBuilder.DropColumn(
                name: "remote_access_kind",
                table: "sync_roots");

            migrationBuilder.DropColumn(
                name: "remote_role",
                table: "sync_roots");
        }
    }
}
