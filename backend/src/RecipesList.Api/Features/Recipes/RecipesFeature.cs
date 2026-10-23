namespace RecipesList.Api.Features.Recipes;

/// <summary>Recipe CRUD and visibility (API.md §4). Implemented in B3.</summary>
internal static class RecipesFeature
{
    public static IServiceCollection AddRecipes(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapRecipes(this IEndpointRouteBuilder api)
    {
        api.MapStub("GET", "/recipes", "ListRecipes", "List recipes visible to the caller");
        api.MapStub("GET", "/recipes/{id:guid}", "GetRecipe", "Get a recipe");
        api.MapStub("POST", "/recipes", "CreateRecipe", "Create a recipe");
        api.MapStub("PUT", "/recipes/{id:guid}", "UpdateRecipe", "Edit a recipe directly");
        api.MapStub("PUT", "/recipes/{id:guid}/visibility", "SetRecipeVisibility", "Change a recipe's visibility");
        api.MapStub("DELETE", "/recipes/{id:guid}", "DeleteRecipe", "Delete or archive a recipe");
        return api;
    }
}
