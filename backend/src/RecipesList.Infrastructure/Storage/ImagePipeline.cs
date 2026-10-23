using RecipesList.Core.Common;
using RecipesList.Core.Images;
using SkiaSharp;

namespace RecipesList.Infrastructure.Storage;

/// <summary>The two WebP renditions of an upload.</summary>
public sealed record ProcessedImage(byte[] Full, int Width, int Height, byte[] Thumb);

/// <summary>
/// Validates and re-encodes uploads (TECHNICAL §3.7): magic bytes (JPEG, PNG, WebP), EXIF orientation applied,
/// at most 1600 px and 400 px on the longest side, WebP quality 82. Re-encoding drops all metadata.
/// </summary>
public static class ImagePipeline
{
    private const int HeaderLength = 12;

    /// <summary>Processes the upload bytes; throws <see cref="DomainValidationException"/> <c>image.invalid_type</c> for anything but a decodable JPEG, PNG or WebP.</summary>
    public static ProcessedImage Process(ReadOnlySpan<byte> data)
    {
        if (data.Length > ImageLimits.MaxUploadBytes)
        {
            throw new ImageTooLargeException();
        }

        if (!HasKnownMagicBytes(data))
        {
            throw Invalid();
        }

        using var bitmap = DecodeOriented(data);
        using var full = ResizeToFit(bitmap, ImageLimits.FullMaxSide);
        using var thumb = ResizeToFit(full, ImageLimits.ThumbMaxSide);
        return new ProcessedImage(EncodeWebp(full), full.Width, full.Height, EncodeWebp(thumb));
    }

    /// <summary>JPEG (FF D8 FF), PNG (89 'PNG' 0D 0A 1A 0A) or WebP ('RIFF' size 'WEBP').</summary>
    public static bool HasKnownMagicBytes(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength)
        {
            return false;
        }

        var jpeg = data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
        var png = data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        var webp = data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8);
        return jpeg || png || webp;
    }

    private static SKBitmap DecodeOriented(ReadOnlySpan<byte> data)
    {
        using var skData = SKData.CreateCopy(data);
        using var codec = SKCodec.Create(skData) ?? throw Invalid();
        var info = codec.Info;
        if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > ImageLimits.MaxSourcePixels)
        {
            throw Invalid();
        }

        var origin = codec.EncodedOrigin;
        using var decoded = SKBitmap.Decode(codec) ?? throw Invalid();
        return ApplyOrigin(decoded, origin);
    }

    // Draws the bitmap upright. EXIF origins 5-8 swap the sides.
    private static SKBitmap ApplyOrigin(SKBitmap source, SKEncodedOrigin origin)
    {
        if (origin is SKEncodedOrigin.TopLeft or SKEncodedOrigin.Default)
        {
            return source.Copy();
        }

        var swap = origin is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var width = swap ? source.Height : source.Width;
        var height = swap ? source.Width : source.Height;
        var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Transparent);
        canvas.Concat(OriginMatrix(origin, source.Width, source.Height));
        canvas.DrawBitmap(source, 0f, 0f, new SKSamplingOptions(SKFilterMode.Linear), null);
        return target;
    }

    // Maps the stored (sensor) pixels to the upright image.
    private static SKMatrix OriginMatrix(SKEncodedOrigin origin, int w, int h) => origin switch
    {
        SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
        SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
        SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
        SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
        SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
        _ => SKMatrix.Identity,
    };

    // Never upscales.
    private static SKBitmap ResizeToFit(SKBitmap source, int maxSide)
    {
        var longest = Math.Max(source.Width, source.Height);
        if (longest <= maxSide)
        {
            return source.Copy();
        }

        var scale = (double)maxSide / longest;
        var width = Math.Max(1, (int)Math.Round(source.Width * scale));
        var height = Math.Max(1, (int)Math.Round(source.Height * scale));
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        return source.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw Invalid();
    }

    private static byte[] EncodeWebp(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Webp, ImageLimits.WebpQuality)
            ?? throw new InvalidOperationException("WebP encoding failed.");
        return encoded.ToArray();
    }

    private static DomainValidationException Invalid() =>
        new(ErrorCodes.ImageInvalidType, message: "Only JPEG, PNG and WebP images are accepted.");
}
