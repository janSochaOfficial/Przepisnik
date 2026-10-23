using RecipesList.Core.Variants;

namespace RecipesList.Api.Features.Variants;

/// <summary>Variants, the variant matrix and selection memory (API.md §5). Implemented in B4.</summary>
internal static class VariantsFeature
{
    public static IServiceCollection AddVariants(this IServiceCollection services)
    {
        services.AddSingleton<IVariantEngine, NotImplementedVariantEngine>();
        return services;
    }

    public static IEndpointRouteBuilder MapVariants(this IEndpointRouteBuilder api)
    {
        api.MapStub("GET", "/recipes/{id:guid}/variants", "ListRecipeVariants", "Variants of a recipe visible to the caller");
        api.MapStub("GET", "/variants", "ListMyVariants", "Variants created by the caller");
        api.MapStub("GET", "/variants/{id:guid}", "GetVariant", "Get a variant");
        api.MapStub("GET", "/variants/{id:guid}/resolved", "GetResolvedVariant", "A variant applied to its base recipe");
        api.MapStub("POST", "/recipes/{id:guid}/variants", "CreateVariant", "Create a variant from an edited copy");
        api.MapStub("PUT", "/variants/{id:guid}", "UpdateVariant", "Edit a variant");
        api.MapStub("DELETE", "/variants/{id:guid}", "DeleteVariant", "Delete a variant");
        api.MapStub("GET", "/recipes/{id:guid}/variant-matrix", "GetVariantMatrix", "The slot/option matrix for auto-match");
        api.MapStub("GET", "/recipes/{id:guid}/my-selection", "GetMySelection", "The caller's remembered picks");
        api.MapStub("PUT", "/recipes/{id:guid}/my-selection", "PutMySelection", "Save the caller's picks and scaling");
        return api;
    }
}
