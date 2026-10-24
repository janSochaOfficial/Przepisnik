using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipesList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class U1_CustomUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "quantity_custom_unit_id",
                table: "recipe_ingredients",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "custom_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    forms = table.Column<string>(type: "jsonb", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    measure_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    amount = table.Column<decimal>(type: "numeric", nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_custom_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_custom_units_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_custom_units_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredients_quantity_custom_unit_id",
                table: "recipe_ingredients",
                column: "quantity_custom_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_units_created_by_id",
                table: "custom_units",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_custom_units_normalized_name_product_id",
                table: "custom_units",
                columns: new[] { "normalized_name", "product_id" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_custom_units_product_id",
                table: "custom_units",
                column: "product_id");

            migrationBuilder.AddForeignKey(
                name: "fk_recipe_ingredients_custom_units_quantity_custom_unit_id",
                table: "recipe_ingredients",
                column: "quantity_custom_unit_id",
                principalTable: "custom_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_recipe_ingredients_custom_units_quantity_custom_unit_id",
                table: "recipe_ingredients");

            migrationBuilder.DropTable(
                name: "custom_units");

            migrationBuilder.DropIndex(
                name: "ix_recipe_ingredients_quantity_custom_unit_id",
                table: "recipe_ingredients");

            migrationBuilder.DropColumn(
                name: "quantity_custom_unit_id",
                table: "recipe_ingredients");
        }
    }
}
