using Microsoft.AspNetCore.Http.HttpResults;
using RecipesList.Core.Common;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;

namespace RecipesList.Api.Features.Recipes;

/// <summary>Recipe CRUD and visibility (API.md §4). The favourite and export/import routes belong to B7 and B6.</summary>
internal static class RecipesFeature
{
    public static IServiceCollection AddRecipes(this IServiceCollection services)
    {
        services.AddScoped<IRecipeService, RecipeService>();
        services.AddScoped<RecipeQueries>();
        return services;
    }

    public static IEndpointRouteBuilder MapRecipes(this IEndpointRouteBuilder api)
    {
        api.MapGet("/recipes", ListAsync)
            .WithName("ListRecipes")
            .WithSummary("List recipes visible to the caller")
            .Produces<PagedDto<RecipeSummaryDto>>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        api.MapGet("/recipes/{id:guid}", GetAsync)
            .WithName("GetRecipe")
            .WithSummary("Get a recipe")
            .Produces<RecipeDetailDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/recipes", CreateAsync)
            .WithName("CreateRecipe")
            .WithSummary("Create a recipe")
            .Produces<RecipeDetailDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        api.MapPut("/recipes/{id:guid}", UpdateAsync)
            .WithName("UpdateRecipe")
            .WithSummary("Edit a recipe directly")
            .Produces<RecipeDetailDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPut("/recipes/{id:guid}/visibility", SetVisibilityAsync)
            .WithName("SetRecipeVisibility")
            .WithSummary("Change a recipe's visibility")
            .Produces<RecipeDetailDto>()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/recipes/{id:guid}", DeleteAsync)
            .WithName("DeleteRecipe")
            .WithSummary("Delete or archive a recipe")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<Ok<PagedDto<RecipeSummaryDto>>> ListAsync(
        RecipeQueries queries,
        string? q,
        string? tags,
        string? author,
        string? visibility,
        int? page,
        int? pageSize,
        string? sort,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        var tagIds = new List<Guid>();
        foreach (var part in (tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var tagId))
            {
                tagIds.Add(tagId);
            }
            else
            {
                errors["tags"] = ["invalid"];
            }
        }

        if (author is not null && author != "me" && !Guid.TryParse(author, out _))
        {
            errors["author"] = ["invalid"];
        }

        VisibilityLevel? level = null;
        if (visibility is not null)
        {
            if (Enum.TryParse<VisibilityLevel>(visibility, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                level = parsed;
            }
            else
            {
                errors["visibility"] = ["invalid"];
            }
        }

        if (sort is not (null or "newest" or "title"))
        {
            errors["sort"] = ["invalid"];
        }

        if (page is < 1)
        {
            errors["page"] = ["out_of_range"];
        }

        if (pageSize is < 1 or > RecipeQueries.MaxPageSize)
        {
            errors["pageSize"] = ["out_of_range"];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(ErrorCodes.ValidationFailed, errors);
        }

        var result = await queries.ListAsync(
            new RecipeListQuery(q, tagIds, author, level, page ?? 1, pageSize ?? RecipeQueries.DefaultPageSize, sort ?? "newest"),
            ct);
        return TypedResults.Ok(result);
    }

    private static async Task<Ok<RecipeDetailDto>> GetAsync(Guid id, RecipeQueries queries, CancellationToken ct) =>
        TypedResults.Ok(await queries.GetAsync(id, ct) ?? throw new NotFoundException(ErrorCodes.RecipeNotFound));

    private static async Task<Created<RecipeDetailDto>> CreateAsync(
        RecipeWriteRequest request,
        IRecipeService recipes,
        RecipeQueries queries,
        CancellationToken ct)
    {
        var visibility = request.Visibility
            ?? throw Missing("visibility");
        var id = await recipes.CreateAsync(request.ToInput(), visibility, ct);
        var detail = await queries.GetAsync(id, ct) ?? throw new NotFoundException(ErrorCodes.RecipeNotFound);
        return TypedResults.Created($"/api/v1/recipes/{id}", detail);
    }

    private static async Task<Ok<RecipeDetailDto>> UpdateAsync(
        Guid id,
        RecipeWriteRequest request,
        IRecipeService recipes,
        RecipeQueries queries,
        CancellationToken ct)
    {
        var version = request.Version ?? throw Missing("version");
        var deleteVariants = request.DeleteVariants ?? throw Missing("deleteVariants");
        await recipes.UpdateAsync(id, request.ToInput(), version, deleteVariants, ct);
        return TypedResults.Ok(await queries.GetAsync(id, ct) ?? throw new NotFoundException(ErrorCodes.RecipeNotFound));
    }

    private static async Task<Ok<RecipeDetailDto>> SetVisibilityAsync(
        Guid id,
        SetVisibilityRequest request,
        IRecipeService recipes,
        RecipeQueries queries,
        CancellationToken ct)
    {
        await recipes.SetVisibilityAsync(id, request.Visibility, ct);
        return TypedResults.Ok(await queries.GetAsync(id, ct) ?? throw new NotFoundException(ErrorCodes.RecipeNotFound));
    }

    private static async Task<NoContent> DeleteAsync(Guid id, IRecipeService recipes, CancellationToken ct)
    {
        await recipes.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static DomainValidationException Missing(string field) =>
        new(ErrorCodes.ValidationFailed, new Dictionary<string, string[]> { [field] = ["required"] });
}
