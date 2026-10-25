namespace RecipesList.Core.Recipes;

/// <summary>
/// When a user's last counted cook of a recipe happened, for the 12-hour rule (D-43). One row per user and recipe,
/// whatever the variant. Deleted with the user or the recipe.
/// </summary>
public sealed class UserRecipeCook
{
    public Guid UserId { get; set; }

    public Guid RecipeId { get; set; }

    public DateTimeOffset LastCountedAt { get; set; }
}
