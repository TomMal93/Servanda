using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servanda.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "app_state",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false),
                    content_epoch = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_state", x => x.id);
                    table.CheckConstraint("ck_app_state_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "areas",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    icon_key = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    accent_key = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    module_key = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    availability = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    is_hidden = table.Column<bool>(type: "INTEGER", nullable: false),
                    archived_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_areas", x => x.id);
                    table.CheckConstraint("ck_areas_availability", "availability IN ('active', 'planned')");
                    table.CheckConstraint("ck_areas_revision", "revision > 0");
                    table.CheckConstraint("ck_areas_sort_order", "sort_order >= 0");
                });

            migrationBuilder.CreateTable(
                name: "ordering_scopes",
                columns: table => new
                {
                    scope_key = table.Column<string>(type: "TEXT", maxLength: 220, nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ordering_scopes", x => x.scope_key);
                    table.CheckConstraint("ck_ordering_scopes_revision", "revision > 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_areas_module_key",
                table: "areas",
                column: "module_key",
                unique: true,
                filter: "availability = 'active' AND archived_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_areas_sort_order",
                table: "areas",
                column: "sort_order",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "app_state");

            migrationBuilder.DropTable(
                name: "areas");

            migrationBuilder.DropTable(
                name: "ordering_scopes");
        }
    }
}
