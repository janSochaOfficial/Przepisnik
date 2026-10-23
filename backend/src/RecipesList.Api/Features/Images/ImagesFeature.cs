using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using RecipesList.Api.Hosting;
using RecipesList.Api.Middleware;
using RecipesList.Core.Common;
using RecipesList.Core.Images;
using RecipesList.Infrastructure.Storage;

namespace RecipesList.Api.Features.Images;

/// <summary>Image upload and download (API.md §3).</summary>
internal static class ImagesFeature
{
    private const string FormField = "file";

    // Room for the multipart envelope around a 10 MB file; the file itself is checked against the exact limit.
    private const long MaxRequestBytes = ImageLimits.MaxUploadBytes + 1024 * 1024;

    public static IServiceCollection AddImages(this IServiceCollection services)
    {
        services.AddSingleton<IImageStorage>(sp =>
            new LocalDiskImageStorage(sp.GetRequiredService<IConfiguration>().GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions()));
        services.AddScoped<OrphanImageCleaner>();
        services.AddHostedService<OrphanImageCleanupService>();
        return services;
    }

    public static IEndpointRouteBuilder MapImages(this IEndpointRouteBuilder api)
    {
        api.MapPost("/images", UploadAsync)
            .WithName("UploadImage")
            .WithSummary("Upload a photo")
            .RequireRateLimiting(RateLimitPolicies.Images)
            .WithMetadata(new RequestSizeLimitAttribute(MaxRequestBytes))
            .Accepts<IFormFile>("multipart/form-data")
            .Produces<ImageInfoDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge);

        api.MapGet("/images/{id:guid}", GetAsync)
            .WithName("GetImage")
            .WithSummary("Download a photo")
            .Produces(StatusCodes.Status200OK, contentType: ImageInfoDto.ContentType)
            .ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<Results<Created<ImageInfoDto>, ProblemHttpResult>> UploadAsync(
        HttpRequest request,
        ICurrentUser currentUser,
        IImageStorage storage,
        IAppDbContext db,
        TimeProvider time,
        CancellationToken ct)
    {
        if (!request.HasFormContentType)
        {
            throw new DomainValidationException(ErrorCodes.ImageInvalidType, message: "Send the photo as multipart/form-data.");
        }

        IFormFile? file;
        try
        {
            file = (await request.ReadFormAsync(ct)).Files.GetFile(FormField);
        }
        catch (BadHttpRequestException e) when (e.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return TooLarge();
        }
        catch (InvalidDataException)
        {
            // The multipart body exceeded the form limits.
            return TooLarge();
        }

        if (file is null)
        {
            throw new DomainValidationException(
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]> { [FormField] = ["required"] });
        }

        if (file.Length > ImageLimits.MaxUploadBytes)
        {
            return TooLarge();
        }

        StoredImage stored;
        await using (var stream = file.OpenReadStream())
        {
            try
            {
                stored = await storage.SaveAsync(stream, ct);
            }
            catch (ImageTooLargeException)
            {
                return TooLarge();
            }
        }

        try
        {
            db.Images.Add(new Image
            {
                Id = stored.Id,
                OwnerId = currentUser.Id,
                Width = stored.Width,
                Height = stored.Height,
                SizeBytes = stored.SizeBytes,
                CreatedAt = time.GetUtcNow(),
            });
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            await storage.DeleteAsync(stored.Id, CancellationToken.None);
            throw;
        }

        var info = ImageInfoDto.From(stored.Id, stored.Width, stored.Height);
        return TypedResults.Created(info.Url, info);
    }

    private static FileStreamHttpResult GetAsync(Guid id, HttpContext http, IImageStorage storage, string? size)
    {
        var rendition = size switch
        {
            null or "full" => ImageSize.Full,
            "thumb" => ImageSize.Thumb,
            _ => throw new DomainValidationException(
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]> { ["size"] = ["invalid"] }),
        };
        var stream = storage.Open(id, rendition)
            ?? throw new NotFoundException(ErrorCodes.ImageNotFound);

        // Image ids are never reused and the content never changes.
        http.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return TypedResults.Stream(stream, ImageInfoDto.ContentType);
    }

    private static ProblemHttpResult TooLarge()
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status413PayloadTooLarge,
            Title = "Payload Too Large",
            Detail = "The image is larger than 10 MB.",
        };
        problem.Extensions[ProblemCodes.CodeExtension] = ErrorCodes.ImageTooLarge;
        return TypedResults.Problem(problem);
    }
}
