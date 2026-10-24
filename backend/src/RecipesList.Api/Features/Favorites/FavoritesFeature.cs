using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using RecipesList.Api.Features.Recipes;
using RecipesList.Api.Features.Variants;
using RecipesList.Core.Common;
using RecipesList.Core.Favorites;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Api.Features.Favorites;

/// <summary>A favourite in the merged list (API.md §5.3 <c>FavoriteItem</c>). <c>Variant</c> is null for recipe entries.</summary>
internal sealed record FavoriteItemDto(
    string Kind,
    DateTimeOffset FavoritedAt,
    RecipeSummaryDto Recipe,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] VariantSummaryResponse? Variant);

/// <summary>Favourites (♥) for recipes and variants (API.md §4, §5, §5.3).</summary>
internal static class FavoritesFeature
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IServiceCollection AddFavorites(this IServiceCollection services)
    {
        services.AddScoped<IFavoriteService, FavoriteService>();
        return services;
    }

    public static IEndpointRouteBuilder MapFavorites(this IEndpointRouteBuilder api)
    {
        api.MapPut("/recipes/{id:guid}/favorite", async (Guid id, IFavoriteService service, CancellationToken ct) =>
            {
                await service.FavoriteRecipeAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("FavoriteRecipe")
            .WithSummary("Mark a recipe as favourite")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/recipes/{id:guid}/favorite", async (Guid id, IFavoriteService service, CancellationToken ct) =>
            {
                await service.UnfavoriteRecipeAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("UnfavoriteRecipe")
            .WithSummary("Remove a recipe from favourites");

        api.MapPut("/variants/{id:guid}/favorite", async (Guid id, IFavoriteService service, CancellationToken ct) =>
            {
                await service.FavoriteVariantAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("FavoriteVariant")
            .WithSummary("Mark a variant as favourite")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapDelete("/variants/{id:guid}/favorite", async (Guid id, IFavoriteService service, CancellationToken ct) =>
            {
                await service.UnfavoriteVariantAsync(id, ct);
                return TypedResults.NoContent();
            })
            .WithName("UnfavoriteVariant")
            .WithSummary("Remove a variant from favourites");

        api.MapGet("/favorites", ListAsync)
            .WithName("ListFavorites")
            .WithSummary("The caller's favourite recipes and variants")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return api;
    }

    private static async Task<Ok<PagedDto<FavoriteItemDto>>> ListAsync(
        int? page,
        int? pageSize,
        IFavoriteService service,
        RecipeQueries recipeQueries,
        IAppDbContext db,
        VisibilityRules visibility,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (page is < 1)
        {
            errors["page"] = ["out_of_range"];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = ["out_of_range"];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(ErrorCodes.ValidationFailed, errors);
        }

        var pageNumber = page ?? 1;
        var size = pageSize ?? DefaultPageSize;
        var keys = await service.ListAsync(pageNumber, size, ct);
        if (keys.Items.Count == 0)
        {
            return TypedResults.Ok(new PagedDto<FavoriteItemDto>([], pageNumber, size, keys.TotalCount));
        }

        var me = currentUser.Id;
        var variantIds = keys.Items.Where(k => k.Kind == FavoriteKind.Variant).Select(k => k.ItemId).ToList();
        var variants = new Dictionary<Guid, VariantSummaryResponse>();
        if (variantIds.Count > 0)
        {
            var rows = await visibility.VisibleVariants(me)
                .Where(v => variantIds.Contains(v.Id))
                .ToSummaryRows(db, visibility.VisibleRecipes(me), me)
                .AsNoTracking()
                .ToListAsync(ct);
            variants = rows.ToDictionary(r => r.Variant.Id, r => VariantSummaryResponse.From(r.ToModel()));
        }

        var recipeIds = keys.Items.Where(k => k.Kind == FavoriteKind.Recipe).Select(k => k.ItemId)
            .Concat(variants.Values.Select(v => v.RecipeId))
            .Distinct()
            .ToList();
        var recipes = (await recipeQueries.SummarizeAsync(visibility.VisibleRecipes(me).Where(r => recipeIds.Contains(r.Id)), ct))
            .ToDictionary(r => r.Id);

        var items = new List<FavoriteItemDto>(keys.Items.Count);
        foreach (var key in keys.Items)
        {
            if (key.Kind == FavoriteKind.Recipe)
            {
                if (recipes.TryGetValue(key.ItemId, out var recipe))
                {
                    items.Add(new FavoriteItemDto("recipe", key.FavoritedAt, recipe, null));
                }
            }
            else if (variants.TryGetValue(key.ItemId, out var variant) && recipes.TryGetValue(variant.RecipeId, out var baseRecipe))
            {
                items.Add(new FavoriteItemDto("variant", key.FavoritedAt, baseRecipe, variant));
            }
        }

        return TypedResults.Ok(new PagedDto<FavoriteItemDto>(items, pageNumber, size, keys.TotalCount));
    }
}
