namespace RecipesList.Api.Features.Favorites;

/// <summary>Favourites (♥) for recipes and variants (API.md §4, §5, §5.3). Implemented in B7.</summary>
internal static class FavoritesFeature
{
    public static IServiceCollection AddFavorites(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapFavorites(this IEndpointRouteBuilder api)
    {
        api.MapStub("PUT", "/recipes/{id:guid}/favorite", "FavoriteRecipe", "Mark a recipe as favourite");
        api.MapStub("DELETE", "/recipes/{id:guid}/favorite", "UnfavoriteRecipe", "Remove a recipe from favourites");
        api.MapStub("PUT", "/variants/{id:guid}/favorite", "FavoriteVariant", "Mark a variant as favourite");
        api.MapStub("DELETE", "/variants/{id:guid}/favorite", "UnfavoriteVariant", "Remove a variant from favourites");
        api.MapStub("GET", "/favorites", "ListFavorites", "The caller's favourite recipes and variants");
        return api;
    }
}
