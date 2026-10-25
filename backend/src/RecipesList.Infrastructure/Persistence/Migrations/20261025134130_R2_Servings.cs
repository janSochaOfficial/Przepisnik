using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipesList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class R2_Servings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "servings_override",
                table: "variants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "servings",
                table: "recipes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_variants_servings_override_range",
                table: "variants",
                sql: "servings_override BETWEEN 1 AND 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_recipes_servings_range",
                table: "recipes",
                sql: "servings BETWEEN 1 AND 100");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_variants_servings_override_range",
                table: "variants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_recipes_servings_range",
                table: "recipes");

            migrationBuilder.DropColumn(
                name: "servings_override",
                table: "variants");

            migrationBuilder.DropColumn(
                name: "servings",
                table: "recipes");
        }
    }
}
