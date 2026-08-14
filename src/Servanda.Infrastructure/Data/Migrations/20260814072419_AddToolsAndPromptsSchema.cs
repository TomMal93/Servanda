using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

// Kod generowany przez EF Core zapisuje kolumny indeksów jako literały tablic.
#pragma warning disable CA1861

namespace Servanda.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddToolsAndPromptsSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    area_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    parent_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.UniqueConstraint("AK_categories_id_area_id", x => new { x.id, x.area_id });
                    table.CheckConstraint("ck_categories_parent", "parent_id IS NULL OR parent_id <> id");
                    table.CheckConstraint("ck_categories_revision", "revision > 0");
                    table.CheckConstraint("ck_categories_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_categories_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_categories_categories_parent_id_area_id",
                        columns: x => new { x.parent_id, x.area_id },
                        principalTable: "categories",
                        principalColumns: new[] { "id", "area_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    area_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    normalized_name = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tags", x => x.id);
                    table.CheckConstraint("ck_tags_revision", "revision > 0");
                    table.ForeignKey(
                        name: "FK_tags_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prompts",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    area_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    category_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    is_favorite = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompts", x => x.id);
                    table.CheckConstraint("ck_prompts_revision", "revision > 0");
                    table.CheckConstraint("ck_prompts_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_prompts_categories_category_id_area_id",
                        columns: x => new { x.category_id, x.area_id },
                        principalTable: "categories",
                        principalColumns: new[] { "id", "area_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tools",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    area_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    category_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 70, nullable: false),
                    description = table.Column<string>(type: "TEXT", maxLength: 280, nullable: false),
                    url = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    group_key = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    revision = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tools", x => x.id);
                    table.CheckConstraint("ck_tools_group_key", "group_key IN ('featured', 'regular')");
                    table.CheckConstraint("ck_tools_revision", "revision > 0");
                    table.CheckConstraint("ck_tools_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_tools_categories_category_id_area_id",
                        columns: x => new { x.category_id, x.area_id },
                        principalTable: "categories",
                        principalColumns: new[] { "id", "area_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prompt_tags",
                columns: table => new
                {
                    prompt_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    tag_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_tags", x => new { x.prompt_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_prompt_tags_prompts_prompt_id",
                        column: x => x.prompt_id,
                        principalTable: "prompts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_prompt_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prompt_variables",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    prompt_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    default_value = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    is_required = table.Column<bool>(type: "INTEGER", nullable: false),
                    is_multiline = table.Column<bool>(type: "INTEGER", nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_variables", x => x.id);
                    table.CheckConstraint("ck_prompt_variables_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_prompt_variables_prompts_prompt_id",
                        column: x => x.prompt_id,
                        principalTable: "prompts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_variants",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    prompt_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    target = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    content = table.Column<string>(type: "TEXT", maxLength: 30000, nullable: false),
                    sort_order = table.Column<int>(type: "INTEGER", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_variants", x => x.id);
                    table.CheckConstraint("ck_prompt_variants_sort_order", "sort_order >= 0");
                    table.ForeignKey(
                        name: "FK_prompt_variants_prompts_prompt_id",
                        column: x => x.prompt_id,
                        principalTable: "prompts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_versions",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    prompt_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    snapshot_json = table.Column<string>(type: "TEXT", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_prompt_versions_prompts_prompt_id",
                        column: x => x.prompt_id,
                        principalTable: "prompts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tool_tags",
                columns: table => new
                {
                    tool_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    tag_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tool_tags", x => new { x.tool_id, x.tag_id });
                    table.ForeignKey(
                        name: "FK_tool_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_tool_tags_tools_tool_id",
                        column: x => x.tool_id,
                        principalTable: "tools",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_usage",
                columns: table => new
                {
                    id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: false),
                    prompt_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    variant_id = table.Column<string>(type: "TEXT", maxLength: 26, nullable: true),
                    prompt_title = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    variant_name = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_usage", x => x.id);
                    table.ForeignKey(
                        name: "FK_prompt_usage_prompt_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "prompt_variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prompt_usage_prompts_prompt_id",
                        column: x => x.prompt_id,
                        principalTable: "prompts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_categories_area_id_parent_id_sort_order",
                table: "categories",
                columns: new[] { "area_id", "parent_id", "sort_order" },
                unique: true,
                filter: "parent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_categories_area_id_sort_order",
                table: "categories",
                columns: new[] { "area_id", "sort_order" },
                unique: true,
                filter: "parent_id IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_categories_parent_id_area_id",
                table: "categories",
                columns: new[] { "parent_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "IX_prompt_tags_tag_id",
                table: "prompt_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_usage_prompt_id",
                table: "prompt_usage",
                column: "prompt_id");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_usage_used_at",
                table: "prompt_usage",
                column: "used_at");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_usage_variant_id",
                table: "prompt_usage",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_variables_prompt_id_name",
                table: "prompt_variables",
                columns: new[] { "prompt_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_variables_prompt_id_sort_order",
                table: "prompt_variables",
                columns: new[] { "prompt_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_variants_prompt_id_sort_order",
                table: "prompt_variants",
                columns: new[] { "prompt_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompt_versions_prompt_id_created_at",
                table: "prompt_versions",
                columns: new[] { "prompt_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_category_id_area_id",
                table: "prompts",
                columns: new[] { "category_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "IX_prompts_category_id_sort_order",
                table: "prompts",
                columns: new[] { "category_id", "sort_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_prompts_is_favorite",
                table: "prompts",
                column: "is_favorite");

            migrationBuilder.CreateIndex(
                name: "IX_tags_area_id_normalized_name",
                table: "tags",
                columns: new[] { "area_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_tool_tags_tag_id",
                table: "tool_tags",
                column: "tag_id");

            migrationBuilder.CreateIndex(
                name: "IX_tools_category_id_area_id",
                table: "tools",
                columns: new[] { "category_id", "area_id" });

            migrationBuilder.CreateIndex(
                name: "IX_tools_category_id_group_key_sort_order",
                table: "tools",
                columns: new[] { "category_id", "group_key", "sort_order" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE tool_search USING fts5(
                    entity_id UNINDEXED,
                    name,
                    tags,
                    category_path,
                    url,
                    description,
                    tokenize='unicode61 remove_diacritics 0',
                    prefix='2 3 4');
                """);
            migrationBuilder.Sql(
                """
                CREATE VIRTUAL TABLE prompt_search USING fts5(
                    entity_id UNINDEXED,
                    title,
                    tags,
                    category_path,
                    variant_names,
                    variant_targets,
                    description,
                    variant_content,
                    tokenize='unicode61 remove_diacritics 0',
                    prefix='2 3 4');
                """);

            // Etap P4 aktywuje moduły narzędzi i promptów w istniejącej bazie.
            migrationBuilder.Sql(
                """
                UPDATE areas
                SET availability = 'active', revision = revision + 1
                WHERE module_key IN ('tools', 'prompts')
                  AND availability = 'planned'
                  AND archived_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE areas
                SET availability = 'planned', revision = revision + 1
                WHERE module_key IN ('tools', 'prompts')
                  AND availability = 'active';
                """);
            migrationBuilder.Sql("DROP TABLE IF EXISTS prompt_search;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS tool_search;");

            migrationBuilder.DropTable(
                name: "prompt_tags");

            migrationBuilder.DropTable(
                name: "prompt_usage");

            migrationBuilder.DropTable(
                name: "prompt_variables");

            migrationBuilder.DropTable(
                name: "prompt_versions");

            migrationBuilder.DropTable(
                name: "tool_tags");

            migrationBuilder.DropTable(
                name: "prompt_variants");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "tools");

            migrationBuilder.DropTable(
                name: "prompts");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}

#pragma warning restore CA1861
