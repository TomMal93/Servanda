using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servanda.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class AddAreaVisibilityIndex : Migration
{
    private static readonly string[] IndexColumns = ["archived_at", "is_hidden", "sort_order"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_areas_archived_at_is_hidden_sort_order",
            table: "areas",
            columns: IndexColumns);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_areas_archived_at_is_hidden_sort_order",
            table: "areas");
    }
}
