namespace RecipesList.Core.Images;

/// <summary>An uploaded photo; the files live in <see cref="IImageStorage"/> (TECHNICAL §3.7).</summary>
public sealed class Image
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>The uploader. Only they may reference the image in new content (API.md §3).</summary>
    public Guid OwnerId { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public long SizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Which rendition of an image to read.</summary>
public enum ImageSize
{
    Full,
    Thumb,
}

/// <summary>The result of storing an upload.</summary>
public sealed record StoredImage(Guid Id, int Width, int Height, long SizeBytes);

/// <summary>Stores re-encoded images (implemented in B3 by <c>LocalDiskImageStorage</c>).</summary>
public interface IImageStorage
{
    /// <summary>Validates, re-encodes and stores an upload; returns its id and dimensions.</summary>
    Task<StoredImage> SaveAsync(Stream s, CancellationToken ct);

    /// <summary>Opens a stored rendition, or null when it does not exist.</summary>
    Stream? Open(Guid id, ImageSize size);

    /// <summary>Deletes every rendition of an image.</summary>
    Task DeleteAsync(Guid id, CancellationToken ct);
}
