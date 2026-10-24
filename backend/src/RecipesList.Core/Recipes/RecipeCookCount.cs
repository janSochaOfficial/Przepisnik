namespace RecipesList.Core.Recipes;

/// <summary>
/// How many times a recipe was deducted from a fridge. Kept apart from <see cref="Recipe"/> so that cooking never
/// changes the recipe's row version (D-28). No row means zero. Deleted with the recipe.
/// </summary>
public sealed class RecipeCookCount
{
    public Guid RecipeId { get; set; }

    public int Count { get; set; }
}
