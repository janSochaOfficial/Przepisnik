namespace RecipesList.Api.Features.Images;

/// <summary>A stored photo (API.md §3). <see cref="Url"/> is <c>/api/v1/images/{id}</c>.</summary>
/// <param name="Id">The image id.</param>
/// <param name="Url">The full rendition (at most 1600 px).</param>
/// <param name="ThumbnailUrl">The thumbnail rendition (at most 400 px).</param>
/// <param name="Width">Width of the full rendition in pixels.</param>
/// <param name="Height">Height of the full rendition in pixels.</param>
internal sealed record ImageInfoDto(Guid Id, string Url, string ThumbnailUrl, int Width, int Height)
{
    public const string ContentType = "image/webp";

    public static ImageInfoDto From(Guid id, int width, int height) =>
        new(id, $"/api/v1/images/{id:D}", $"/api/v1/images/{id:D}?size=thumb", width, height);
}
