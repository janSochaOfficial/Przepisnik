using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Favorites;
using RecipesList.Core.Fridges;
using RecipesList.Core.Images;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.ShoppingLists;
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
