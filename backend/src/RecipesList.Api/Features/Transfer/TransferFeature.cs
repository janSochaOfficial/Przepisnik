using RecipesList.Api.Hosting;

namespace RecipesList.Api.Features.Transfer;

/// <summary>Recipe JSON export and import (API.md §4, §8). Implemented in B6.</summary>
internal static class TransferFeature
{
    public static IServiceCollection AddTransfer(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapTransfer(this IEndpointRouteBuilder api)
    {
        api.MapStub("GET", "/recipes/{id:guid}/export", "ExportRecipe", "Export a recipe with its visible variants");
        api.MapStub("POST", "/recipes/import", "ImportRecipe", "Import a recipe export file").RequireRateLimiting(RateLimitPolicies.Import);
        return api;
    }
}
