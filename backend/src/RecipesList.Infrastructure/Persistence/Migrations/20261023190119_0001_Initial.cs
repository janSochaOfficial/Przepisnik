using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RecipesList.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class _0001_Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,")
                .Annotation("Npgsql:PostgresExtension:unaccent", ",,");

            migrationBuilder.CreateTable(
                name: "tags",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "utensils",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_utensils", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deductions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fridge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: true),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    undone_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lines = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deductions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fridge_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fridge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fridge_invites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fridge_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fridge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_amount = table.Column<decimal>(type: "numeric", nullable: false),
                    remaining_percent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    added_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fridge_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fridge_members",
                columns: table => new
                {
                    fridge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fridge_members", x => new { x.fridge_id, x.user_id });
                });

            migrationBuilder.CreateTable(
                name: "fridges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_fridges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    google_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    nickname = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    normalized_nickname = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_admin = table.Column<bool>(type: "boolean", nullable: false),
                    liquid_units = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    default_fridge_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_fridges_default_fridge_id",
                        column: x => x.default_fridge_id,
                        principalTable: "fridges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_images", x => x.id);
                    table.ForeignKey(
                        name: "fk_images_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    measure_kind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_class = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    has_sizes = table.Column<bool>(type: "boolean", nullable: false),
                    allow_half = table.Column<bool>(type: "boolean", nullable: false),
                    grams_per_ml = table.Column<decimal>(type: "numeric", nullable: true),
                    grams_per_piece = table.Column<decimal>(type: "numeric", nullable: true),
                    default_package_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    piece_forms = table.Column<string>(type: "jsonb", nullable: true),
                    created_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.ForeignKey(
                        name: "fk_products_users_created_by_id",
                        column: x => x.created_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "recipes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    normalized_title = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    cover_image_id = table.Column<Guid>(type: "uuid", nullable: true),
                    prep_minutes = table.Column<int>(type: "integer", nullable: true),
                    cook_minutes = table.Column<int>(type: "integer", nullable: true),
                    visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    cook_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipes", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipes_images_cover_image_id",
                        column: x => x.cover_image_id,
                        principalTable: "images",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_recipes_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_preferences",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    to_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    weight = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_preferences", x => new { x.user_id, x.from_product_id, x.to_product_id });
                    table.ForeignKey(
                        name: "fk_product_preferences_products_from_product_id",
                        column: x => x.from_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_preferences_products_to_product_id",
                        column: x => x.to_product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_preferences_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shopping_list_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fridge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: true),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    amount = table.Column<decimal>(type: "numeric", nullable: true),
                    @checked = table.Column<bool>(name: "checked", type: "boolean", nullable: false),
                    added_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shopping_list_items", x => x.id);
                    table.CheckConstraint("ck_shopping_list_items_product_xor_text", "(product_id IS NULL) <> (text IS NULL)");
                    table.ForeignKey(
                        name: "fk_shopping_list_items_fridges_fridge_id",
                        column: x => x.fridge_id,
                        principalTable: "fridges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shopping_list_items_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_shopping_list_items_users_added_by_id",
                        column: x => x.added_by_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recipe_favorites",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_favorites", x => new { x.user_id, x.recipe_id });
                    table.ForeignKey(
                        name: "fk_recipe_favorites_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_favorites_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredients",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity_value = table.Column<decimal>(type: "numeric", nullable: true),
                    quantity_unit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    quantity_size = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    base_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    optional = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_ingredients", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipe_ingredients_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recipe_ingredients_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_selections",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    picks = table.Column<string>(type: "jsonb", nullable: false),
                    scaling = table.Column<string>(type: "jsonb", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_selections", x => new { x.user_id, x.recipe_id });
                    table.ForeignKey(
                        name: "fk_recipe_selections_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_selections_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    blocks = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipe_steps_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_tags",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tag_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_tags", x => new { x.recipe_id, x.tag_id });
                    table.ForeignKey(
                        name: "fk_recipe_tags_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_tags_tags_tag_id",
                        column: x => x.tag_id,
                        principalTable: "tags",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "recipe_utensils",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    utensil_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_utensils", x => new { x.recipe_id, x.utensil_id });
                    table.ForeignKey(
                        name: "fk_recipe_utensils_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_recipe_utensils_utensils_utensil_id",
                        column: x => x.utensil_id,
                        principalTable: "utensils",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    visibility = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ingredient_ops = table.Column<string>(type: "jsonb", nullable: false),
                    step_ops = table.Column<string>(type: "jsonb", nullable: false),
                    utensil_ids_added = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    utensil_ids_removed = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    prep_minutes = table.Column<int>(type: "integer", nullable: true),
                    cook_minutes = table.Column<int>(type: "integer", nullable: true),
                    cook_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variants", x => x.id);
                    table.ForeignKey(
                        name: "fk_variants_recipes_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_variants_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "variant_favorites",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    variant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_variant_favorites", x => new { x.user_id, x.variant_id });
                    table.ForeignKey(
                        name: "fk_variant_favorites_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_variant_favorites_variants_variant_id",
                        column: x => x.variant_id,
                        principalTable: "variants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_deductions_fridge_id_created_at",
                table: "deductions",
                columns: new[] { "fridge_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_deductions_recipe_id",
                table: "deductions",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_deductions_user_id",
                table: "deductions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_deductions_variant_id",
                table: "deductions",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_fridge_invites_created_by_id",
                table: "fridge_invites",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_fridge_invites_fridge_id",
                table: "fridge_invites",
                column: "fridge_id");

            migrationBuilder.CreateIndex(
                name: "ix_fridge_invites_token",
                table: "fridge_invites",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fridge_items_added_by_id",
                table: "fridge_items",
                column: "added_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_fridge_items_fridge_id_product_id",
                table: "fridge_items",
                columns: new[] { "fridge_id", "product_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fridge_items_product_id",
                table: "fridge_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_fridge_members_user_id_fridge_id",
                table: "fridge_members",
                columns: new[] { "user_id", "fridge_id" });

            migrationBuilder.CreateIndex(
                name: "ix_fridges_owner_id",
                table: "fridges",
                column: "owner_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_images_created_at",
                table: "images",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_images_owner_id",
                table: "images",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_preferences_from_product_id",
                table: "product_preferences",
                column: "from_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_preferences_to_product_id",
                table: "product_preferences",
                column: "to_product_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_category",
                table: "products",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_products_created_by_id",
                table: "products",
                column: "created_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_normalized_name",
                table: "products",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_normalized_name1",
                table: "products",
                column: "normalized_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_favorites_recipe_id",
                table: "recipe_favorites",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_favorites_user_id_created_at",
                table: "recipe_favorites",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredients_product_id",
                table: "recipe_ingredients",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredients_recipe_id_position",
                table: "recipe_ingredients",
                columns: new[] { "recipe_id", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_selections_recipe_id",
                table: "recipe_selections",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_steps_recipe_id_section_position",
                table: "recipe_steps",
                columns: new[] { "recipe_id", "section", "position" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_tags_tag_id_recipe_id",
                table: "recipe_tags",
                columns: new[] { "tag_id", "recipe_id" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_utensils_utensil_id",
                table: "recipe_utensils",
                column: "utensil_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_author_id_created_at",
                table: "recipes",
                columns: new[] { "author_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_recipes_cover_image_id",
                table: "recipes",
                column: "cover_image_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipes_normalized_title",
                table: "recipes",
                column: "normalized_title")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_recipes_visibility_created_at",
                table: "recipes",
                columns: new[] { "visibility", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_items_added_by_id",
                table: "shopping_list_items",
                column: "added_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_items_fridge_id_checked",
                table: "shopping_list_items",
                columns: new[] { "fridge_id", "checked" });

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_items_product_id",
                table: "shopping_list_items",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_tags_normalized_name",
                table: "tags",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tags_normalized_name1",
                table: "tags",
                column: "normalized_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_users_default_fridge_id",
                table: "users",
                column: "default_fridge_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_google_subject",
                table: "users",
                column: "google_subject",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_normalized_nickname",
                table: "users",
                column: "normalized_nickname",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_utensils_normalized_name",
                table: "utensils",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_utensils_normalized_name1",
                table: "utensils",
                column: "normalized_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_variant_favorites_user_id_created_at",
                table: "variant_favorites",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_variant_favorites_variant_id",
                table: "variant_favorites",
                column: "variant_id");

            migrationBuilder.CreateIndex(
                name: "ix_variants_author_id_created_at",
                table: "variants",
                columns: new[] { "author_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_variants_recipe_id_author_id_name",
                table: "variants",
                columns: new[] { "recipe_id", "author_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_deductions_fridges_fridge_id",
                table: "deductions",
                column: "fridge_id",
                principalTable: "fridges",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_deductions_recipes_recipe_id",
                table: "deductions",
                column: "recipe_id",
                principalTable: "recipes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_deductions_users_user_id",
                table: "deductions",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_deductions_variants_variant_id",
                table: "deductions",
                column: "variant_id",
                principalTable: "variants",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_fridge_invites_fridges_fridge_id",
                table: "fridge_invites",
                column: "fridge_id",
                principalTable: "fridges",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_fridge_invites_users_created_by_id",
                table: "fridge_invites",
                column: "created_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fridge_items_fridges_fridge_id",
                table: "fridge_items",
                column: "fridge_id",
                principalTable: "fridges",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_fridge_items_products_product_id",
                table: "fridge_items",
                column: "product_id",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fridge_items_users_added_by_id",
                table: "fridge_items",
                column: "added_by_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_fridge_members_fridges_fridge_id",
                table: "fridge_members",
                column: "fridge_id",
                principalTable: "fridges",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_fridge_members_users_user_id",
                table: "fridge_members",
                column: "user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_fridges_users_owner_id",
                table: "fridges",
                column: "owner_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_fridges_default_fridge_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "deductions");

            migrationBuilder.DropTable(
                name: "fridge_invites");

            migrationBuilder.DropTable(
                name: "fridge_items");

            migrationBuilder.DropTable(
                name: "fridge_members");

            migrationBuilder.DropTable(
                name: "product_preferences");

            migrationBuilder.DropTable(
                name: "recipe_favorites");

            migrationBuilder.DropTable(
                name: "recipe_ingredients");

            migrationBuilder.DropTable(
                name: "recipe_selections");

            migrationBuilder.DropTable(
                name: "recipe_steps");

            migrationBuilder.DropTable(
                name: "recipe_tags");

            migrationBuilder.DropTable(
                name: "recipe_utensils");

            migrationBuilder.DropTable(
                name: "shopping_list_items");

            migrationBuilder.DropTable(
                name: "variant_favorites");

            migrationBuilder.DropTable(
                name: "tags");

            migrationBuilder.DropTable(
                name: "utensils");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "variants");

            migrationBuilder.DropTable(
                name: "recipes");

            migrationBuilder.DropTable(
                name: "images");

            migrationBuilder.DropTable(
                name: "fridges");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
