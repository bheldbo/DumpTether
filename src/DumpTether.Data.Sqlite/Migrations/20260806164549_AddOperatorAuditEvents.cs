using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddOperatorAuditEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "operator_audit_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "TEXT", nullable: false),
                    actor = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    action = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    target_user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    target_email = table.Column<string>(type: "TEXT", maxLength: 320, nullable: false),
                    reason = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_operator_audit_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_operator_audit_events_occurred_at",
                table: "operator_audit_events",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "IX_operator_audit_events_target_user_id",
                table: "operator_audit_events",
                column: "target_user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "operator_audit_events");
        }
    }
}
