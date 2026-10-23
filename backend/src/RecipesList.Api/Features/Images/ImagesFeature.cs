using RecipesList.Api.Hosting;
using RecipesList.Core.Images;

namespace RecipesList.Api.Features.Images;

/// <summary>Image upload and download (API.md §3). Implemented in B3.</summary>
internal static class ImagesFeature
{
    public static IServiceCollection AddImages(this IServiceCollection services)
    {
        services.AddSingleton<IImageStorage, NotImplementedImageStorage>();
        return services;
    }

    public static IEndpointRouteBuilder MapImages(this IEndpointRouteBuilder api)
    {
        api.MapStub("POST", "/images", "UploadImage", "Upload a photo").RequireRateLimiting(RateLimitPolicies.Images);
        api.MapStub("GET", "/images/{id:guid}", "GetImage", "Download a photo");
        return api;
    }
}
