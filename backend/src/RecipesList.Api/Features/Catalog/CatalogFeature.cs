using Microsoft.AspNetCore.Http.HttpResults;
using RecipesList.Core.Catalog;
using RecipesList.Infrastructure.Seeding;

namespace RecipesList.Api.Features.Catalog;

/// <summary>Products, tags and utensils (API.md §2).</summary>
internal static class CatalogFeature
{
    public static IServiceCollection AddCatalog(this IServiceCollection services)
    {
        services.AddScoped<ProductCatalog>();
        services.AddScoped<ProductAdmin>();
        services.AddScoped<NameCatalogs>();
        services.AddScoped<CatalogSeeder>();
        services.AddHostedService<CatalogSeedService>();
        return services;
    }

    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder api)
    {
        MapProducts(api);
        MapAdmin(api);
        MapTags(api);
        MapUtensils(api);
        return api;
    }

    private static void MapProducts(IEndpointRouteBuilder api)
    {
        api.MapGet("/products", async Task<Ok<CatalogProductDto[]>> (string? q, ProductCategory? category, int? limit, ProductCatalog catalog, CancellationToken ct) =>
                TypedResults.Ok((await catalog.SearchAsync(q, category, limit, ct)).Select(CatalogProductDto.From).ToArray()))
            .WithName("SearchProducts")
            .WithSummary("Search the product catalog");

        api.MapGet("/products/{id:guid}", async Task<Ok<CatalogProductDto>> (Guid id, ProductCatalog catalog, CancellationToken ct) =>
                TypedResults.Ok(CatalogProductDto.From(await catalog.GetAsync(id, ct))))
            .WithName("GetProduct")
            .WithSummary("Get a product")
            .ProducesProblem(StatusCodes.Status404NotFound);

        api.MapPost("/products", async Task<Created<CatalogProductDto>> (ProductWriteRequest request, ProductCatalog catalog, CancellationToken ct) =>
            {
                var product = await catalog.CreateAsync(request.ToInput(), ct);
                return TypedResults.Created($"/api/v1/products/{product.Id}", CatalogProductDto.From(product));
            })
            .WithName("CreateProduct")
            .WithSummary("Add a product to the catalog")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapAdmin(IEndpointRouteBuilder api)
    {
        api.MapPut("/admin/products/{id:guid}", async Task<Ok<CatalogProductDto>> (Guid id, ProductWriteRequest request, ProductAdmin admin, CancellationToken ct) =>
                TypedResults.Ok(CatalogProductDto.From(await admin.UpdateAsync(id, request.ToInput(), ct))))
            .WithName("AdminUpdateProduct")
            .WithSummary("Edit a product (admin)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        api.MapPost("/admin/products/{id:guid}/merge", async Task<NoContent> (Guid id, MergeProductRequest request, ProductAdmin admin, CancellationToken ct) =>
            {
                await admin.MergeAsync(id, request.TargetId, ct);
                return TypedResults.NoContent();
            })
            .WithName("AdminMergeProduct")
            .WithSummary("Merge a product into another (admin)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapTags(IEndpointRouteBuilder api)
    {
        api.MapGet("/tags", async Task<Ok<CatalogTagDto[]>> (string? q, int? limit, NameCatalogs catalogs, CancellationToken ct) =>
                TypedResults.Ok((await catalogs.SearchTagsAsync(q, limit, ct)).Select(CatalogTagDto.From).ToArray()))
            .WithName("SearchTags")
            .WithSummary("Search tags");

        api.MapPost("/tags", async Task<Results<Created<CatalogTagDto>, Ok<CatalogTagDto>>> (NameWriteRequest request, NameCatalogs catalogs, CancellationToken ct) =>
            {
                var (tag, created) = await catalogs.CreateTagAsync(request.Name, ct);
                var dto = CatalogTagDto.From(tag);
                return created ? TypedResults.Created($"/api/v1/tags/{tag.Id}", dto) : TypedResults.Ok(dto);
            })
            .WithName("CreateTag")
            .WithSummary("Create a tag, or return the existing one")
            .ProducesValidationProblem();
    }

    private static void MapUtensils(IEndpointRouteBuilder api)
    {
        api.MapGet("/utensils", async Task<Ok<CatalogUtensilDto[]>> (string? q, int? limit, NameCatalogs catalogs, CancellationToken ct) =>
                TypedResults.Ok((await catalogs.SearchUtensilsAsync(q, limit, ct)).Select(CatalogUtensilDto.From).ToArray()))
            .WithName("SearchUtensils")
            .WithSummary("Search utensils");

        api.MapPost("/utensils", async Task<Results<Created<CatalogUtensilDto>, Ok<CatalogUtensilDto>>> (NameWriteRequest request, NameCatalogs catalogs, CancellationToken ct) =>
            {
                var (utensil, created) = await catalogs.CreateUtensilAsync(request.Name, ct);
                var dto = CatalogUtensilDto.From(utensil);
                return created ? TypedResults.Created($"/api/v1/utensils/{utensil.Id}", dto) : TypedResults.Ok(dto);
            })
            .WithName("CreateUtensil")
            .WithSummary("Create a utensil, or return the existing one")
            .ProducesValidationProblem();
    }
}
