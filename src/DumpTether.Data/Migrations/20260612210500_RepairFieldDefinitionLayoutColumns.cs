using DumpTether.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DumpTether.Data.Migrations;

[DbContext(typeof(DumpTetherDbContext))]
[Migration("20260612210500_RepairFieldDefinitionLayoutColumns")]
public partial class RepairFieldDefinitionLayoutColumns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE field_definitions
                ADD COLUMN IF NOT EXISTS layout_row integer NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS layout_column integer NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS layout_row_span integer NOT NULL DEFAULT 1,
                ADD COLUMN IF NOT EXISTS layout_column_span integer NOT NULL DEFAULT 1;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE field_definitions
                DROP COLUMN IF EXISTS layout_column_span,
                DROP COLUMN IF EXISTS layout_row_span,
                DROP COLUMN IF EXISTS layout_column,
                DROP COLUMN IF EXISTS layout_row;
            """);
    }
}
