using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSyncMappingFailureState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_attempted_at",
                table: "sync_mappings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_error",
                table: "sync_mappings",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_attempted_at",
                table: "sync_mappings");

            migrationBuilder.DropColumn(
                name: "last_error",
                table: "sync_mappings");
        }
    }
}
