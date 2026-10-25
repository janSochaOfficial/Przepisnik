using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipesList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class U2_ShoppingItemQuantity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "quantity_custom_unit_id",
                table: "shopping_list_items",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quantity_size",
                table: "shopping_list_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "quantity_unit",
                table: "shopping_list_items",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "quantity_value",
                table: "shopping_list_items",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_items_quantity_custom_unit_id",
                table: "shopping_list_items",
                column: "quantity_custom_unit_id");

            migrationBuilder.AddForeignKey(
                name: "fk_shopping_list_items_custom_units_quantity_custom_unit_id",
                table: "shopping_list_items",
                column: "quantity_custom_unit_id",
                principalTable: "custom_units",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_shopping_list_items_custom_units_quantity_custom_unit_id",
                table: "shopping_list_items");

            migrationBuilder.DropIndex(
                name: "ix_shopping_list_items_quantity_custom_unit_id",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "quantity_custom_unit_id",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "quantity_size",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "quantity_unit",
                table: "shopping_list_items");

            migrationBuilder.DropColumn(
                name: "quantity_value",
                table: "shopping_list_items");
        }
    }
}
