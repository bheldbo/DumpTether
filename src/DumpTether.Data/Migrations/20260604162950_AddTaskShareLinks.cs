using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "accepted_at",
                table: "task_item_shares",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "expires_at",
                table: "task_item_shares",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "token_hash",
                table: "task_item_shares",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE task_item_shares
                SET accepted_at = created_at
                WHERE token_hash IS NULL
                  AND accepted_at IS NULL
                  AND revoked_at IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_task_item_shares_token_hash",
                table: "task_item_shares",
                column: "token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_task_item_shares_token_hash",
                table: "task_item_shares");

            migrationBuilder.DropColumn(
                name: "accepted_at",
                table: "task_item_shares");

            migrationBuilder.DropColumn(
                name: "expires_at",
                table: "task_item_shares");

            migrationBuilder.DropColumn(
                name: "token_hash",
                table: "task_item_shares");
        }
    }
}
