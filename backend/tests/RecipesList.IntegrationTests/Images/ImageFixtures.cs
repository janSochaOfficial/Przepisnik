using SkiaSharp;

namespace RecipesList.IntegrationTests.Images;

/// <summary>Small generated images for the image tests (no binary fixtures in the repository).</summary>
internal static class ImageFixtures
{
    public static byte[] Png(int width, int height) => Encode(width, height, SKEncodedImageFormat.Png);

    public static byte[] Jpeg(int width, int height) => Encode(width, height, SKEncodedImageFormat.Jpeg);

    public static byte[] Webp(int width, int height) => Encode(width, height, SKEncodedImageFormat.Webp);

    /// <summary>A JPEG with an EXIF APP1 segment that carries the given orientation tag (1-8).</summary>
    public static byte[] JpegWithOrientation(int width, int height, byte orientation)
    {
        var jpeg = Jpeg(width, height);
        byte[] exif =
        [
            0xFF, 0xE1, 0x00, 0x22, // APP1, length 34
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0,
            (byte)'M', (byte)'M', 0x00, 0x2A, 0x00, 0x00, 0x00, 0x08, // big-endian TIFF header, IFD at 8
            0x00, 0x01, // one entry
            0x01, 0x12, 0x00, 0x03, 0x00, 0x00, 0x00, 0x01, 0x00, orientation, 0x00, 0x00, // Orientation, SHORT, 1 value
            0x00, 0x00, 0x00, 0x00, // no next IFD
        ];

        // SOI (2 bytes), then our APP1, then the rest of the file.
        return [.. jpeg.AsSpan(0, 2), .. exif, .. jpeg.AsSpan(2)];
    }

    public static (int Width, int Height) SizeOf(byte[] encoded)
    {
        using var bitmap = SKBitmap.Decode(encoded);
        return (bitmap.Width, bitmap.Height);
    }

    private static byte[] Encode(int width, int height, SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.CornflowerBlue);
            using var paint = new SKPaint { Color = SKColors.OrangeRed };
            canvas.DrawRect(0, 0, width / 2f, height / 2f, paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }
}
