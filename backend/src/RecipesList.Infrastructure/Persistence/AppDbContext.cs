using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Favorites;
using RecipesList.Core.Fridges;
using RecipesList.Core.Images;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.ShoppingLists;
using RecipesList.Core.Units;
using RecipesList.Core.Users;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Infrastructure.Persistence;

/// <summary>The EF Core context. Entity mappings live in <c>Configurations/&lt;Entity&gt;Configuration.cs</c>.</summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Utensil> Utensils => Set<Utensil>();

    public DbSet<Image> Images => Set<Image>();

    public DbSet<Recipe> Recipes => Set<Recipe>();

    public DbSet<RecipeTag> RecipeTags => Set<RecipeTag>();

    public DbSet<RecipeUtensil> RecipeUtensils => Set<RecipeUtensil>();

    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();

    public DbSet<RecipeStep> RecipeSteps => Set<RecipeStep>();

    public DbSet<Variant> Variants => Set<Variant>();

    public async Task<bool> TryClaimCookAsync(Guid userId, Guid recipeId, DateTimeOffset now, DateTimeOffset notAfter, CancellationToken ct)
    {
        var rows = await Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO user_recipe_cooks (user_id, recipe_id, last_counted_at) VALUES ({userId}, {recipeId}, {now})
            ON CONFLICT (user_id, recipe_id) DO UPDATE SET last_counted_at = EXCLUDED.last_counted_at
            WHERE user_recipe_cooks.last_counted_at <= {notAfter}
            """,
            ct);
        return rows > 0;
    }

    public async Task IncrementCookCountsAsync(Guid recipeId, Guid? variantId, CancellationToken ct)
    {
        var baseCooks = variantId is null ? 1 : 0;
        await Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO recipe_cook_counts (recipe_id, count, base_count) VALUES ({recipeId}, 1, {baseCooks})
            ON CONFLICT (recipe_id) DO UPDATE
            SET count = recipe_cook_counts.count + 1, base_count = recipe_cook_counts.base_count + {baseCooks}
            """,
            ct);

        if (variantId is { } variant)
        {
            await Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO variant_cook_counts (variant_id, count) VALUES ({variant}, 1)
                ON CONFLICT (variant_id) DO UPDATE SET count = variant_cook_counts.count + 1
                """,
                ct);
        }
    }

    public DbSet<UserRecipeCook> UserRecipeCooks => Set<UserRecipeCook>();

    public DbSet<RecipeCookCount> RecipeCookCounts => Set<RecipeCookCount>();

    public DbSet<VariantCookCount> VariantCookCounts => Set<VariantCookCount>();

    public DbSet<RecipeFavorite> RecipeFavorites => Set<RecipeFavorite>();

    public DbSet<VariantFavorite> VariantFavorites => Set<VariantFavorite>();

    public DbSet<RecipeSelection> RecipeSelections => Set<RecipeSelection>();

    public DbSet<ProductPreference> ProductPreferences => Set<ProductPreference>();

    public DbSet<Fridge> Fridges => Set<Fridge>();

    public DbSet<FridgeMember> FridgeMembers => Set<FridgeMember>();

    public DbSet<FridgeInvite> FridgeInvites => Set<FridgeInvite>();

    public DbSet<FridgeItem> FridgeItems => Set<FridgeItem>();

    public DbSet<Deduction> Deductions => Set<Deduction>();

    public DbSet<ShoppingListItem> ShoppingListItems => Set<ShoppingListItem>();

    public DbSet<CustomUnit> CustomUnits => Set<CustomUnit>();

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => SaveChangesAsync(ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pg_trgm");
        modelBuilder.HasPostgresExtension("unaccent");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Enums are stored as their names so the data stays readable in SQL and survives reordering.
        StoreAsString<MeasureKind>(configurationBuilder);
        StoreAsString<DisplayClass>(configurationBuilder);
        StoreAsString<ProductCategory>(configurationBuilder);
        StoreAsString<Unit>(configurationBuilder);
        StoreAsString<PieceSize>(configurationBuilder);
        StoreAsString<LiquidUnits>(configurationBuilder);
        StoreAsString<StepSection>(configurationBuilder);
        StoreAsString<FridgeRole>(configurationBuilder);
        StoreAsString<VisibilityLevel>(configurationBuilder);
    }

    private static void StoreAsString<TEnum>(ModelConfigurationBuilder builder)
        where TEnum : struct, Enum
    {
        // Also applies to TEnum? properties.
        builder.Properties<TEnum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
