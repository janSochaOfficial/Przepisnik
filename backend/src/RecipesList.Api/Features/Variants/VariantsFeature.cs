using Microsoft.AspNetCore.Http.HttpResults;
using RecipesList.Core.Common;
using RecipesList.Core.Variants;

namespace RecipesList.Api.Features.Variants;

/// <summary>Variants, the variant matrix and selection memory (API.md §5).</summary>
internal static class VariantsFeature
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static IServiceCollection AddVariants(this IServiceCollection services)
    {
        services.AddSingleton<IVariantEngine>(new VariantEngine());
        services.AddScoped<SelectionMemory>();
        services.AddScoped<IVariantService, VariantService>();
        return services;
    }

    public static IEndpointRouteBuilder MapVariants(this IEndpointRouteBuilder api)
    {
        api.MapGet("/recipes/{id:guid}/variants", ListRecipeVariantsAsync)
            .WithName("ListRecipeVariants")
            .WithSummary("Variants of a recipe visible to the caller")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/variants", ListMyVariantsAsync)
            .WithName("ListMyVariants")
            .WithSummary("Variants created by the caller")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        api.MapGet("/variants/{id:guid}", GetVariantAsync)
            .WithName("GetVariant")
            .WithSummary("Get a variant")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/variants/{id:guid}/resolved", GetResolvedVariantAsync)
            .WithName("GetResolvedVariant")
            .WithSummary("A variant applied to its base recipe")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/recipes/{id:guid}/variants", CreateVariantAsync)
            .WithName("CreateVariant")
            .WithSummary("Create a variant from an edited copy")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPut("/variants/{id:guid}", UpdateVariantAsync)
            .WithName("UpdateVariant")
            .WithSummary("Edit a variant")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapDelete("/variants/{id:guid}", DeleteVariantAsync)
            .WithName("DeleteVariant")
            .WithSummary("Delete a variant")
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/recipes/{id:guid}/variant-matrix", GetVariantMatrixAsync)
            .WithName("GetVariantMatrix")
            .WithSummary("The slot/option matrix for auto-match")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/recipes/{id:guid}/my-selection", GetMySelectionAsync)
            .WithName("GetMySelection")
            .WithSummary("The caller's remembered picks")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPut("/recipes/{id:guid}/my-selection", PutMySelectionAsync)
            .WithName("PutMySelection")
            .WithSummary("Save the caller's picks and scaling")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return api;
    }

    private static async Task<Ok<VariantSummaryResponse[]>> ListRecipeVariantsAsync(Guid id, IVariantService service, CancellationToken ct)
    {
        var variants = await service.ListForRecipeAsync(id, ct);
        return TypedResults.Ok(variants.Select(VariantSummaryResponse.From).ToArray());
    }

    private static async Task<Ok<VariantSummaryPage>> ListMyVariantsAsync(
        string? author,
        int? page,
        int? pageSize,
        IVariantService service,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (author != "me")
        {
            errors["author"] = [ErrorCodes.ValidationFailed];
        }

        if (page is < 1)
        {
            errors["page"] = [ErrorCodes.ValidationFailed];
        }

        if (pageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [ErrorCodes.ValidationFailed];
        }

        if (errors.Count > 0)
        {
            throw new DomainValidationException(ErrorCodes.ValidationFailed, errors);
        }

        var result = await service.ListMineAsync(page ?? 1, pageSize ?? DefaultPageSize, ct);
        return TypedResults.Ok(new VariantSummaryPage(
            [.. result.Items.Select(VariantSummaryResponse.From)], result.PageNumber, result.PageSize, result.TotalCount));
    }

    private static async Task<Ok<VariantDetailResponse>> GetVariantAsync(Guid id, IVariantService service, CancellationToken ct) =>
        TypedResults.Ok(VariantDetailResponse.From(await service.GetAsync(id, ct)));

    private static async Task<Ok<ResolvedRecipeResponse>> GetResolvedVariantAsync(Guid id, IVariantService service, CancellationToken ct) =>
        TypedResults.Ok(ResolvedRecipeResponse.From(await service.GetResolvedAsync(id, ct)));

    private static async Task<Created<VariantDetailResponse>> CreateVariantAsync(
        Guid id,
        VariantCreateRequest request,
        IVariantService service,
        CancellationToken ct)
    {
        var variant = VariantDetailResponse.From(await service.CreateAsync(id, request.ToInput(), ct));
        return TypedResults.Created($"/api/v1/variants/{variant.Id}", variant);
    }

    private static async Task<Ok<VariantDetailResponse>> UpdateVariantAsync(
        Guid id,
        VariantUpdateRequest request,
        IVariantService service,
        CancellationToken ct) =>
        TypedResults.Ok(VariantDetailResponse.From(await service.UpdateAsync(id, request.ToInput(), request.Version, ct)));

    private static async Task<NoContent> DeleteVariantAsync(Guid id, IVariantService service, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<VariantMatrixResponse>> GetVariantMatrixAsync(Guid id, IVariantService service, CancellationToken ct) =>
        TypedResults.Ok(VariantMatrixResponse.From(await service.GetMatrixAsync(id, ct)));

    private static async Task<Results<Ok<SelectionResponse>, NoContent>> GetMySelectionAsync(Guid id, SelectionMemory selections, CancellationToken ct)
    {
        var selection = await selections.GetAsync(id, ct);
        return selection is null ? TypedResults.NoContent() : TypedResults.Ok(SelectionResponse.From(selection));
    }

    private static async Task<Ok<SelectionResponse>> PutMySelectionAsync(
        Guid id,
        SelectionWriteRequest request,
        SelectionMemory selections,
        CancellationToken ct) =>
        TypedResults.Ok(SelectionResponse.From(await selections.PutAsync(id, request.ToInput(), ct)));
}
