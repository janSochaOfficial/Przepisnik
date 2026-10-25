namespace RecipesList.Core.Recipes;

/// <summary>
/// How many times a recipe was cooked, counted (at most once per user per 12 hours, D-43). Kept apart from
/// <see cref="Recipe"/> so that cooking never changes the recipe's row version (D-28). No row means zero. Deleted with
/// the recipe.
/// </summary>
public sealed class RecipeCookCount
{
    public Guid RecipeId { get; set; }

    /// <summary>The recipe's total: every counted cook, with or without a variant.</summary>
    public int Count { get; set; }

    /// <summary>Counted cooks of the base recipe itself (no variant shown, D-42). Never more than <see cref="Count"/>.</summary>
    public int BaseCount { get; set; }
}
