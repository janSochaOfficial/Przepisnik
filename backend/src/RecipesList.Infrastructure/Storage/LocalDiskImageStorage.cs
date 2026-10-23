using RecipesList.Core.Images;

namespace RecipesList.Infrastructure.Storage;

/// <summary>Configuration section <c>Storage</c> (<c>Storage__ImagesPath</c>).</summary>
public sealed class StorageOptions
{
    public const string Section = "Storage";

    /// <summary>Directory for <c>{id}.webp</c> and <c>{id}_thumb.webp</c>; relative paths resolve against the working directory.</summary>
    public string ImagesPath { get; set; } = "data/images";
}

/// <summary>Stores re-encoded images as files on local disk (TECHNICAL §3.7).</summary>
public sealed class LocalDiskImageStorage : IImageStorage
{
    private readonly string _root;

    public LocalDiskImageStorage(StorageOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _root = Path.GetFullPath(options.ImagesPath);
        Directory.CreateDirectory(_root);
    }

    /// <inheritdoc />
    public async Task<StoredImage> SaveAsync(Stream s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await s.ReadAsync(chunk, ct)) > 0)
        {
            // Stop as soon as the limit is crossed instead of buffering an unbounded body.
            if (buffer.Length + read > ImageLimits.MaxUploadBytes)
            {
                throw new ImageTooLargeException();
            }

            buffer.Write(chunk, 0, read);
        }

        var processed = ImagePipeline.Process(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        var id = Guid.CreateVersion7();

        await File.WriteAllBytesAsync(PathOf(id, ImageSize.Full), processed.Full, ct);
        await File.WriteAllBytesAsync(PathOf(id, ImageSize.Thumb), processed.Thumb, ct);
        return new StoredImage(id, processed.Width, processed.Height, processed.Full.Length);
    }

    /// <inheritdoc />
    public Stream? Open(Guid id, ImageSize size)
    {
        var path = PathOf(id, size);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true) : null;
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(PathOf(id, ImageSize.Full));
        File.Delete(PathOf(id, ImageSize.Thumb));
        return Task.CompletedTask;
    }

    // Ids are Guids, so no user input reaches the file name.
    private string PathOf(Guid id, ImageSize size) =>
        Path.Combine(_root, size == ImageSize.Thumb ? $"{id:D}_thumb.webp" : $"{id:D}.webp");
}
