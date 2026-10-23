namespace RecipesList.Api.Features.Catalog;

/// <summary>Products, tags and utensils (API.md §2). Implemented in B2.</summary>
internal static class CatalogFeature
{
    public static IServiceCollection AddCatalog(this IServiceCollection services) => services;

    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder api)
    {
        api.MapStub("GET", "/products", "SearchProducts", "Search the product catalog");
        api.MapStub("GET", "/products/{id:guid}", "GetProduct", "Get a product");
        api.MapStub("POST", "/products", "CreateProduct", "Add a product to the catalog");
        api.MapStub("PUT", "/admin/products/{id:guid}", "AdminUpdateProduct", "Edit a product (admin)");
        api.MapStub("POST", "/admin/products/{id:guid}/merge", "AdminMergeProduct", "Merge a product into another (admin)");
        api.MapStub("GET", "/tags", "SearchTags", "Search tags");
        api.MapStub("POST", "/tags", "CreateTag", "Create a tag, or return the existing one");
        api.MapStub("GET", "/utensils", "SearchUtensils", "Search utensils");
        api.MapStub("POST", "/utensils", "CreateUtensil", "Create a utensil, or return the existing one");
        return api;
    }
}
