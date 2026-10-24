using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Images;

/// <summary>POST /images and GET /images/{id} (API.md §3).</summary>
public sealed class ImagesEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime, IDisposable
{
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _web;
    private readonly string _imagesPath = Path.Combine(Path.GetTempPath(), "recipes-list-tests", Guid.NewGuid().ToString("N"));
    private readonly ApiClient _client;

    public ImagesEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _web = factory.WithWebHostBuilder(b => b.UseSetting("Storage:ImagesPath", _imagesPath));
        _client = NewClient();
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        await _client.LoginAsync("ola");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        _client.Dispose();
        _web.Dispose();
        if (Directory.Exists(_imagesPath))
        {
            Directory.Delete(_imagesPath, recursive: true);
        }
    }

    [Fact]
    public async Task Upload_Png_Returns201WithImageInfoAndStoresARow()
    {
        var response = await UploadAsync(ImageFixtures.Png(200, 100), "kotlet.png");

        var info = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "ImageInfo");
        var id = info.GetProperty("id").GetGuid();
        info.GetProperty("url").GetString().ShouldBe($"/api/v1/images/{id}");
        info.GetProperty("thumbnailUrl").GetString().ShouldBe($"/api/v1/images/{id}?size=thumb");
        (info.GetProperty("width").GetInt32(), info.GetProperty("height").GetInt32()).ShouldBe((200, 100));
        response.Headers.Location.ShouldNotBeNull();

        await using var db = _factory.CreateDbContext();
        var row = await db.Images.SingleAsync(i => i.Id == id, TestContext.Current.CancellationToken);
        row.OwnerId.ShouldBe(_client.UserId);
        row.Width.ShouldBe(200);
    }

    [Fact]
    public async Task Get_UploadedImage_ReturnsWebpWithImmutableCacheHeaders()
    {
        var info = await UploadOkAsync(ImageFixtures.Jpeg(300, 200));

        var response = await _client.GetAsync(info.GetProperty("url").GetString()!);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("image/webp");
        response.Headers.CacheControl?.ToString().ShouldBe("public, max-age=31536000, immutable");
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        ImageFixtures.SizeOf(bytes).ShouldBe((300, 200));
    }

    [Fact]
    public async Task Get_Thumbnail_IsSmallerThanTheFullImage()
    {
        var info = await UploadOkAsync(ImageFixtures.Png(1000, 500));

        var response = await _client.GetAsync(info.GetProperty("thumbnailUrl").GetString()!);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        ImageFixtures.SizeOf(bytes).ShouldBe((400, 200));
    }

    [Fact]
    public async Task Get_AnotherUsersImage_IsAllowedForAnySignedInUser()
    {
        var info = await UploadOkAsync(ImageFixtures.Png(40, 40));
        using var other = NewClient();
        await other.LoginAsync("kasia");

        var response = await other.GetAsync(info.GetProperty("url").GetString()!);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_UnknownImage_Returns404()
    {
        var response = await _client.GetAsync($"/api/v1/images/{Guid.NewGuid()}");

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "image.not_found");
    }

    [Fact]
    public async Task Get_WithoutSession_Returns401()
    {
        using var anonymous = NewClient();

        var response = await anonymous.GetAsync($"/api/v1/images/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_TextFileRenamedToJpg_Returns400InvalidType()
    {
        var response = await UploadAsync("just some text, not a picture"u8.ToArray(), "cat.jpg", "image/jpeg");

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "image.invalid_type");
        await using var db = _factory.CreateDbContext();
        (await db.Images.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Upload_ElevenMegabytes_Returns413TooLarge()
    {
        var bytes = new byte[11 * 1024 * 1024];
        ImageFixtures.Png(8, 8).CopyTo(bytes, 0);

        var response = await UploadAsync(bytes, "huge.png");

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "image.too_large");
    }

    [Fact]
    public async Task Upload_WithoutFileField_Returns400()
    {
        using var content = new MultipartFormDataContent { { new StringContent("x"), "other" } };

        var response = await _client.Http.PostAsync("/api/v1/images", content, TestContext.Current.CancellationToken);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Upload_AsJson_Returns415()
    {
        var response = await _client.PostJsonAsync("/api/v1/images", new { file = "x" });

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.UnsupportedMediaType, "request.invalid");
    }

    private ApiClient NewClient() =>
        ApiClients.Create(_web);

    private async Task<System.Text.Json.JsonElement> UploadOkAsync(byte[] bytes)
    {
        var response = await UploadAsync(bytes, "photo.bin");
        return await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "ImageInfo");
    }

    private Task<HttpResponseMessage> UploadAsync(byte[] bytes, string fileName, string contentType = "application/octet-stream")
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var content = new MultipartFormDataContent { { file, "file", fileName } };
        return _client.Http.PostAsync("/api/v1/images", content, TestContext.Current.CancellationToken);
    }
}
