using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipesList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class B5_FridgeOwnerNotUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fridges_owner_id",
                table: "fridges");

            migrationBuilder.CreateIndex(
                name: "ix_fridges_owner_id",
                table: "fridges",
                column: "owner_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_fridges_owner_id",
                table: "fridges");

            migrationBuilder.CreateIndex(
                name: "ix_fridges_owner_id",
                table: "fridges",
                column: "owner_id",
                unique: true);
        }
    }
}
