using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipesList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class B8_CookCountTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "recipe_cook_counts",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_cook_counts", x => x.recipe_id);
                    table.ForeignKey(
                        name: "fk_recipe_cook_counts_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "variant_cook_counts",
                columns: table => new
                {
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variant_cook_counts", x => x.variant_id);
                    table.ForeignKey(
                        name: "fk_variant_cook_counts_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // D-28: keep existing counts. Only non-zero counts get a row; a missing row reads as 0.
            migrationBuilder.Sql("INSERT INTO recipe_cook_counts (recipe_id, count) SELECT id, cook_count FROM recipes WHERE cook_count > 0;");
            migrationBuilder.Sql("INSERT INTO variant_cook_counts (variant_id, count) SELECT id, cook_count FROM variants WHERE cook_count > 0;");

            migrationBuilder.DropColumn(
                name: "cook_count",
                table: "variants");

            migrationBuilder.DropColumn(
                name: "cook_count",
                table: "recipes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cook_count",
                table: "variants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "cook_count",
                table: "recipes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE recipes r SET cook_count = c.count FROM recipe_cook_counts c WHERE c.recipe_id = r.id;");
            migrationBuilder.Sql("UPDATE variants v SET cook_count = c.count FROM variant_cook_counts c WHERE c.variant_id = v.id;");

            migrationBuilder.DropTable(
                name: "recipe_cook_counts");

            migrationBuilder.DropTable(
                name: "variant_cook_counts");
        }
    }
}
