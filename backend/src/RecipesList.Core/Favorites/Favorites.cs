namespace RecipesList.Core.Favorites;

/// <summary>A user's ♥ on a recipe (PROJECT §4.6). Deleted with the recipe.</summary>
public sealed class RecipeFavorite
{
    public Guid UserId { get; set; }

    public Guid RecipeId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A user's ♥ on a variant (PROJECT §4.6). Deleted with the variant.</summary>
public sealed class VariantFavorite
{
    public Guid UserId { get; set; }

    public Guid VariantId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
