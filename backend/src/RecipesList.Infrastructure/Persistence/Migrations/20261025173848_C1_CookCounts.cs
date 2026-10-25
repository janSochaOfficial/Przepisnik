using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipesList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class C1_CookCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "base_count",
                table: "recipe_cook_counts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // D-42: past cooks can't be told apart, so the base gets what its variants don't account for. Counts of
            // deleted variants are gone, so those recipes' base count can come out a little high (accepted, C1 Report).
            migrationBuilder.Sql(
                """
                UPDATE recipe_cook_counts rc
                SET base_count = GREATEST(0, rc.count - COALESCE((
                    SELECT SUM(vc.count)
                    FROM variant_cook_counts vc
                    JOIN variants v ON v.id = vc.variant_id
                    WHERE v.recipe_id = rc.recipe_id), 0));
                """);

            migrationBuilder.CreateTable(
                name: "user_recipe_cooks",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_counted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_recipe_cooks", x => new { x.user_id, x.recipe_id });
                    table.ForeignKey(
                        name: "fk_user_recipe_cooks_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_recipe_cooks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_user_recipe_cooks_recipe_id",
                table: "user_recipe_cooks",
                column: "recipe_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_recipe_cooks");

            migrationBuilder.DropColumn(
                name: "base_count",
                table: "recipe_cook_counts");
        }
    }
}
