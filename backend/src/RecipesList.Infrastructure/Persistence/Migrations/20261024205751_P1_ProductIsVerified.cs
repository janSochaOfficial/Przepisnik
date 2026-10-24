using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipesList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class P1_ProductIsVerified : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_verified",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Seeded products and those created by admins count as verified (PROJECT §7.1, D-32).
            migrationBuilder.Sql(
                "UPDATE products SET is_verified = true WHERE created_by_id IS NULL OR created_by_id IN (SELECT id FROM users WHERE is_admin);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_verified",
                table: "products");
        }
    }
}
