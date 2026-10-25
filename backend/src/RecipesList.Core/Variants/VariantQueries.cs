using RecipesList.Core.Common;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Variants;

/// <summary>A variant with the per-caller data of a <c>VariantSummary</c>, projected in one SQL query.</summary>
public sealed record VariantSummaryRow(Variant Variant, string RecipeTitle, string? AuthorNickname, bool IsFavorite, bool BaseAvailable, int CookCount)
{
    /// <summary>The summary; <c>changeCount</c> is computed here because the ops are jsonb.</summary>
    public VariantSummaryModel ToModel() => new(
        Variant.Id,
        Variant.RecipeId,
        RecipeTitle,
        Variant.Name,
        Variant.Description,
        Variant.AuthorId,
        AuthorNickname ?? "",
        Variant.Visibility,
        IsFavorite,
        BaseAvailable,
        CookCount,
        ChangeCount(Variant),
        Variant.CreatedAt,
        Variant.UpdatedAt);

    /// <summary>Operations + utensil changes + time and servings overrides.</summary>
    public static int ChangeCount(Variant variant)
    {
        ArgumentNullException.ThrowIfNull(variant);
        return variant.IngredientOps.Count + variant.StepOps.Count + variant.UtensilIdsAdded.Count + variant.UtensilIdsRemoved.Count
            + (variant.PrepMinutes is null ? 0 : 1) + (variant.CookMinutes is null ? 0 : 1) + (variant.ServingsOverride is null ? 0 : 1);
    }
}

/// <summary>Reusable variant projections (also for the favourites list, B7).</summary>
public static class VariantQueries
{
    /// <summary>
    /// Projects variants to summary rows for <paramref name="userId"/>: the caller's ♥ and whether the base recipe is
    /// visible to them come from correlated subqueries, so a list is one statement (no N+1).
    /// </summary>
    public static IQueryable<VariantSummaryRow> ToSummaryRows(
        this IQueryable<Variant> variants,
        IAppDbContext db,
        IQueryable<Recipe> visibleRecipes,
        Guid userId)
    {
        ArgumentNullException.ThrowIfNull(db);
        var favorites = db.VariantFavorites;
        var cookCounts = db.VariantCookCounts;
        return variants.Select(v => new VariantSummaryRow(
            v,
            v.Recipe!.Title,
            v.Author!.Nickname,
            favorites.Any(f => f.UserId == userId && f.VariantId == v.Id),
            visibleRecipes.Any(r => r.Id == v.RecipeId),
            cookCounts.Where(c => c.VariantId == v.Id).Select(c => c.Count).FirstOrDefault()));
    }
}
