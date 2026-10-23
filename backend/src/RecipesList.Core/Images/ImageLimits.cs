using RecipesList.Core.Common;

namespace RecipesList.Core.Images;

/// <summary>Fixed limits of the image pipeline (TECHNICAL §3.7).</summary>
public static class ImageLimits
{
    /// <summary>Largest accepted upload.</summary>
    public const long MaxUploadBytes = 10L * 1024 * 1024;

    /// <summary>Longest side of the full rendition.</summary>
    public const int FullMaxSide = 1600;

    /// <summary>Longest side of the thumbnail.</summary>
    public const int ThumbMaxSide = 400;

    /// <summary>WebP quality of both renditions.</summary>
    public const int WebpQuality = 82;

    /// <summary>Largest accepted source image, in pixels (guards against decompression bombs).</summary>
    public const long MaxSourcePixels = 100_000_000;

    /// <summary>Uploads older than this and not referenced anywhere are deleted by the cleanup job.</summary>
    public static readonly TimeSpan OrphanGracePeriod = TimeSpan.FromHours(24);
}

/// <summary>The upload exceeds <see cref="ImageLimits.MaxUploadBytes"/> → 413 <c>image.too_large</c>.</summary>
public sealed class ImageTooLargeException()
    : DomainException(ErrorCodes.ImageTooLarge, "The image is larger than 10 MB.");
