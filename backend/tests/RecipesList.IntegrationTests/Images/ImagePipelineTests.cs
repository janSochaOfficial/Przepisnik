using RecipesList.Core.Common;
using RecipesList.Core.Images;
using RecipesList.Infrastructure.Storage;

namespace RecipesList.IntegrationTests.Images;

/// <summary>The decode/orient/resize/encode pipeline (TECHNICAL §3.7) on small generated images.</summary>
public sealed class ImagePipelineTests
{
    private static readonly byte[] RiffWebpHeader = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8];

    [Theory]
    [InlineData("jpeg")]
    [InlineData("png")]
    [InlineData("webp")]
    public void Process_SupportedFormat_ProducesWebp(string format)
    {
        var source = format switch
        {
            "jpeg" => ImageFixtures.Jpeg(64, 48),
            "png" => ImageFixtures.Png(64, 48),
            _ => ImageFixtures.Webp(64, 48),
        };

        var result = ImagePipeline.Process(source);

        result.Full.AsSpan(0, 4).SequenceEqual("RIFF"u8).ShouldBeTrue();
        result.Full.AsSpan(8, 4).SequenceEqual("WEBP"u8).ShouldBeTrue();
        result.Thumb.AsSpan(8, 4).SequenceEqual("WEBP"u8).ShouldBeTrue();
        (result.Width, result.Height).ShouldBe((64, 48));
    }

    [Fact]
    public void Process_LargeImage_FitsTheFullAndThumbnailLimits()
    {
        var result = ImagePipeline.Process(ImageFixtures.Png(3200, 1600));

        (result.Width, result.Height).ShouldBe((1600, 800));
        ImageFixtures.SizeOf(result.Full).ShouldBe((1600, 800));
        ImageFixtures.SizeOf(result.Thumb).ShouldBe((400, 200));
    }

    [Fact]
    public void Process_SmallImage_IsNotUpscaled()
    {
        var result = ImagePipeline.Process(ImageFixtures.Png(100, 60));

        ImageFixtures.SizeOf(result.Full).ShouldBe((100, 60));
        ImageFixtures.SizeOf(result.Thumb).ShouldBe((100, 60));
    }

    [Fact]
    public void Process_TallImage_LimitsTheLongestSide()
    {
        var result = ImagePipeline.Process(ImageFixtures.Png(500, 2000));

        ImageFixtures.SizeOf(result.Full).ShouldBe((400, 1600));
        ImageFixtures.SizeOf(result.Thumb).ShouldBe((100, 400));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public void Process_ExifOrientationThatRotates_SwapsTheSides(byte orientation)
    {
        var result = ImagePipeline.Process(ImageFixtures.JpegWithOrientation(80, 40, orientation));

        (result.Width, result.Height).ShouldBe((40, 80));
    }

    [Fact]
    public void Process_ExifOrientationThatOnlyMirrors_KeepsTheSides()
    {
        var result = ImagePipeline.Process(ImageFixtures.JpegWithOrientation(80, 40, 2));

        (result.Width, result.Height).ShouldBe((80, 40));
    }

    [Fact]
    public void Process_TextRenamedToImage_IsRejectedAsInvalidType()
    {
        var bytes = "this is just a text file renamed to cat.jpg"u8.ToArray();

        var error = Should.Throw<DomainValidationException>(() => ImagePipeline.Process(bytes));

        error.Code.ShouldBe(ErrorCodes.ImageInvalidType);
    }

    [Fact]
    public void Process_ValidMagicBytesButGarbageBody_IsRejectedAsInvalidType()
    {
        var bytes = RiffWebpHeader.Concat(new byte[64]).ToArray();

        var error = Should.Throw<DomainValidationException>(() => ImagePipeline.Process(bytes));

        error.Code.ShouldBe(ErrorCodes.ImageInvalidType);
    }

    [Fact]
    public void Process_GifHeader_IsRejectedAsInvalidType()
    {
        var bytes = "GIF89a"u8.ToArray().Concat(new byte[32]).ToArray();

        Should.Throw<DomainValidationException>(() => ImagePipeline.Process(bytes)).Code.ShouldBe(ErrorCodes.ImageInvalidType);
    }

    [Fact]
    public void Process_MoreThanTenMegabytes_IsTooLarge()
    {
        var bytes = new byte[ImageLimits.MaxUploadBytes + 1];

        Should.Throw<ImageTooLargeException>(() => ImagePipeline.Process(bytes));
    }

    [Fact]
    public async Task LocalDiskImageStorage_SaveOpenDelete_RoundTrips()
    {
        var root = Path.Combine(Path.GetTempPath(), "recipes-list-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new LocalDiskImageStorage(new StorageOptions { ImagesPath = root });
            await using var upload = new MemoryStream(ImageFixtures.Png(120, 90));

            var stored = await storage.SaveAsync(upload, TestContext.Current.CancellationToken);

            (stored.Width, stored.Height).ShouldBe((120, 90));
            File.Exists(Path.Combine(root, $"{stored.Id}.webp")).ShouldBeTrue();
            File.Exists(Path.Combine(root, $"{stored.Id}_thumb.webp")).ShouldBeTrue();
            await using (var full = storage.Open(stored.Id, ImageSize.Full))
            {
                full.ShouldNotBeNull();
            }

            await storage.DeleteAsync(stored.Id, TestContext.Current.CancellationToken);

            storage.Open(stored.Id, ImageSize.Full).ShouldBeNull();
            storage.Open(stored.Id, ImageSize.Thumb).ShouldBeNull();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
