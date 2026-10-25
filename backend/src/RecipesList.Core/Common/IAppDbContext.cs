using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Favorites;
using RecipesList.Core.Fridges;
using RecipesList.Core.Images;
using RecipesList.Core.Recipes;
using RecipesList.Core.ShoppingLists;
using RecipesList.Core.Units;
using RecipesList.Core.Users;
using RecipesList.Core.Variants;

namespace RecipesList.Core.Common;

/// <summary>
/// The persistence abstraction services use directly (no repository layer, TECHNICAL §3.4).
/// Implemented by <c>AppDbContext</c> in Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<Product> Products { get; }

    DbSet<Tag> Tags { get; }

    DbSet<Utensil> Utensils { get; }

    DbSet<Image> Images { get; }

    DbSet<Recipe> Recipes { get; }

    DbSet<RecipeTag> RecipeTags { get; }

    DbSet<RecipeUtensil> RecipeUtensils { get; }

    DbSet<RecipeIngredient> RecipeIngredients { get; }

    DbSet<RecipeStep> RecipeSteps { get; }

    DbSet<Variant> Variants { get; }

    DbSet<RecipeCookCount> RecipeCookCounts { get; }

    DbSet<VariantCookCount> VariantCookCounts { get; }

    DbSet<RecipeFavorite> RecipeFavorites { get; }

    DbSet<VariantFavorite> VariantFavorites { get; }

    DbSet<RecipeSelection> RecipeSelections { get; }

    DbSet<ProductPreference> ProductPreferences { get; }

    DbSet<Fridge> Fridges { get; }

    DbSet<FridgeMember> FridgeMembers { get; }

    DbSet<FridgeInvite> FridgeInvites { get; }

    DbSet<FridgeItem> FridgeItems { get; }

    DbSet<Deduction> Deductions { get; }

    DbSet<ShoppingListItem> ShoppingListItems { get; }

    DbSet<CustomUnit> CustomUnits { get; }

    /// <summary>
    /// The 12-hour rule in one atomic statement (<c>INSERT ... ON CONFLICT DO UPDATE ... WHERE last_counted_at &lt;=
    /// <paramref name="notAfter"/></c>): records <paramref name="now"/> as the user's last counted cook of the recipe
    /// and returns true, or leaves the row alone and returns false when the last one is newer. Two simultaneous calls
    /// cannot both win: the second waits for the first's row lock and then sees its timestamp.
    /// </summary>
    Task<bool> TryClaimCookAsync(Guid userId, Guid recipeId, DateTimeOffset now, DateTimeOffset notAfter, CancellationToken ct);

    /// <summary>
    /// Adds one counted cook with atomic upserts joining the current transaction: the recipe's total, plus either the
    /// recipe's base count (no variant) or the variant's count. The versioned recipe and variant rows are not touched
    /// (D-28).
    /// </summary>
    Task IncrementCookCountsAsync(Guid recipeId, Guid? variantId, CancellationToken ct);

    DbSet<UserRecipeCook> UserRecipeCooks { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);
}
