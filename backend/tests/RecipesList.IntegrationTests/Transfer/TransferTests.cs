using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Transfer;
using RecipesList.IntegrationTests.Images;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Transfer;

/// <summary>GET /recipes/{id}/export and POST /recipes/import (API.md §4, §8; PROJECT §4.7).</summary>
public sealed class TransferTests : IClassFixture<ApiFactory>, IAsyncLifetime, IDisposable
{
    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _web;
    private readonly string _imagesPath = Path.Combine(Path.GetTempPath(), "recipes-list-tests", Guid.NewGuid().ToString("N"));

    private ApiClient _ania = null!;
    private ApiClient _bartek = null!;
    private ApiClient _carol = null!;
    private Guid _flour;
    private Guid _butter;
    private Guid _sugar;
    private Guid _onion;
    private Guid _dinner;
    private Guid _pan;

    public TransferTests(ApiFactory factory)
    {
        _factory = factory;
        _web = factory.WithWebHostBuilder(b => b.UseSetting("Storage:ImagesPath", _imagesPath));
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _ania = NewClient();
        _bartek = NewClient();
        _carol = NewClient();
        await _ania.LoginAsync("ania");
        await _bartek.LoginAsync("bartek");
        await _carol.LoginAsync("carol");

        await using var db = _factory.CreateDbContext();
        _flour = AddProduct(db, "Mąka", MeasureKind.Mass, DisplayClass.Weight);
        _butter = AddProduct(db, "Masło", MeasureKind.Mass, DisplayClass.Weight);
        _sugar = AddProduct(db, "Cukier", MeasureKind.Mass, DisplayClass.Weight);
        _onion = AddProduct(db, "Cebula", MeasureKind.Count, DisplayClass.Piece, gramsPerPiece: 150m, hasSizes: true);
        var dinner = new Tag { Name = "obiad", NormalizedName = "obiad" };
        var quick = new Tag { Name = Tag.QuickTagName, NormalizedName = Tag.QuickTagName, IsSystem = true };
        var pan = new Utensil { Name = "patelnia", NormalizedName = "patelnia" };
        db.Tags.AddRange(dinner, quick);
        db.Utensils.Add(pan);
        _dinner = dinner.Id;
        _pan = pan.Id;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose()
    {
        _ania.Dispose();
        _bartek.Dispose();
        _carol.Dispose();
        _web.Dispose();
        if (Directory.Exists(_imagesPath))
        {
            Directory.Delete(_imagesPath, recursive: true);
        }
    }

    [Fact]
    public async Task RoundTrip_ExportImportExport_IsEqualModuloKeysTimestampsAndAuthors()
    {
        var recipeId = await CreateRichRecipeWithTwoVariantsAsync();

        var first = await ExportAsync(_ania, recipeId);
        first["variants"]!.AsArray().Count.ShouldBe(2);
        var imported = await ImportOkAsync(_carol, first);
        var second = await ExportAsync(_carol, imported.GetProperty("id").GetGuid());

        // Photos are re-encoded on import, so they are compared by size; everything else must be identical.
        Normalize(second).ToJsonString().ShouldBe(Normalize(first).ToJsonString());
        second["recipe"]!["authorNickname"]!.GetValue<string>().ShouldBe("ania");
        second["variants"]!.AsArray().Select(v => v!["authorNickname"]!.GetValue<string>()).ShouldBe(["bartek", "ania"]);
    }

    [Fact]
    public async Task Export_ContainsKeysFullProductsImagesAndNoIdsOrVisibility()
    {
        var recipeId = await CreateRichRecipeWithTwoVariantsAsync();

        var response = await _ania.GetAsync($"/api/v1/recipes/{recipeId}/export");

        var body = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "RecipeExport");
        response.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition.FileName.ShouldBe("nalesniki-z-maslem.json");
        body.GetProperty("format").GetString().ShouldBe("recipes-list/recipe");
        body.GetProperty("formatVersion").GetInt32().ShouldBe(1);
        var text = body.GetRawText();
        text.ShouldNotContain(recipeId.ToString());
        text.ShouldNotContain("visibility");
        text.ShouldNotContain("isArchived");
        body.GetProperty("images").EnumerateObject().Select(i => i.Name).ShouldBe(["img1", "img2", "img3"]);
        var flour = body.GetProperty("products").EnumerateArray().First(p => p.GetProperty("name").GetString() == "Mąka");
        flour.GetProperty("measureKind").GetString().ShouldBe("mass");
        flour.GetProperty("key").GetString().ShouldBe("p1");
        body.GetProperty("recipe").GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ShouldBe(["obiad"]);
        body.GetProperty("recipe").GetProperty("ingredients")[0].GetProperty("key").GetString().ShouldBe("i1");
    }

    [Fact]
    public async Task Export_OnlyIncludesVariantsVisibleToTheCaller()
    {
        var recipeId = await CreateRichRecipeWithTwoVariantsAsync();
        var recipe = await GetDetailAsync(_ania, recipeId);
        await PostVariantAsync(_bartek, recipeId, "Prywatny bartka", recipe, visibility: "private", change: c => c["cookMinutes"] = 99);

        var asCarol = await ExportAsync(_carol, recipeId);
        var asAnia = await ExportAsync(_ania, recipeId);
        var asBartek = await ExportAsync(_bartek, recipeId);

        asCarol["variants"]!.AsArray().Select(v => v!["name"]!.GetValue<string>()).ShouldBe(["Bez masła", "Mniej mąki"]);
        asAnia["variants"]!.AsArray().Count.ShouldBe(2);
        asBartek["variants"]!.AsArray().Select(v => v!["name"]!.GetValue<string>()).ShouldBe(["Bez masła", "Mniej mąki", "Prywatny bartka"]);
    }

    [Fact]
    public async Task Export_RecipeTheCallerCannotSee_Returns404()
    {
        var recipe = await CreateRecipeAsync(_ania, "Sekret", visibility: "private");

        var response = await _bartek.GetAsync($"/api/v1/recipes/{recipe.GetProperty("id").GetGuid()}/export");

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "recipe.not_found");
    }

    [Fact]
    public async Task Import_CreatesPrivateRecipeAndVariantsOwnedByTheImporter()
    {
        var recipeId = await CreateRichRecipeWithTwoVariantsAsync();
        var file = await ExportAsync(_ania, recipeId);

        var response = await _carol.PostJsonAsync("/api/v1/recipes/import", file);

        var recipe = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "RecipeDetail");
        var id = recipe.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/recipes/{id}");
        id.ShouldNotBe(recipeId);
        recipe.GetProperty("visibility").GetString().ShouldBe("private");
        recipe.GetProperty("author").GetProperty("nickname").GetString().ShouldBe("carol");
        recipe.GetProperty("description").GetString().ShouldBe($"Prosty obiad\n\n{ImportFooter.Prefix}ania");
        recipe.GetProperty("variantCount").GetInt32().ShouldBe(2);
        recipe.GetProperty("coverImage").ValueKind.ShouldBe(JsonValueKind.Object);

        await using var db = _factory.CreateDbContext();
        var variants = await db.Variants.Where(v => v.RecipeId == id).OrderBy(v => v.CreatedAt).ToListAsync(TestContext.Current.CancellationToken);
        variants.Select(v => v.AuthorId).Distinct().ShouldBe([_carol.UserId]);
        variants.ShouldAllBe(v => v.Visibility == RecipesList.Core.Visibility.VisibilityLevel.Private);
        variants[0].Description.ShouldBe($"{ImportFooter.Prefix}bartek");
        var imageIds = await db.Images.Where(i => i.OwnerId == _carol.UserId).Select(i => i.Id).ToListAsync(TestContext.Current.CancellationToken);
        imageIds.Count.ShouldBe(3);
        imageIds.ShouldContain(recipe.GetProperty("coverImage").GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Import_UnknownProduct_CreatesItAndExistingNameInOtherCaseIsReused()
    {
        var file = await ExportAsync(_ania, (await CreateRecipeWithFlourAsync()).GetProperty("id").GetGuid());
        var products = file["products"]!.AsArray();
        products[0]!["name"] = "MĄKA";
        products.Add(JsonNode.Parse("""{"key":"p9","name":"Szafran","category":"spices","measureKind":"mass","displayClass":"weight","hasSizes":false,"allowHalf":false,"gramsPerMl":null,"gramsPerPiece":null,"defaultPackageAmount":null,"pieceForms":null}"""));
        file["recipe"]!["ingredients"]!.AsArray().Add(JsonNode.Parse("""{"key":"i9","productKey":"p9","quantity":{"value":1,"unit":"g"},"note":null,"optional":true}"""));
        await using var before = _factory.CreateDbContext();
        var productCount = await before.Products.CountAsync(TestContext.Current.CancellationToken);

        await ImportOkAsync(_carol, file);

        await using var db = _factory.CreateDbContext();
        (await db.Products.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(productCount + 1);
        var saffron = await db.Products.SingleAsync(p => p.Name == "Szafran", TestContext.Current.CancellationToken);
        saffron.Category.ShouldBe(ProductCategory.Spices);
        saffron.NormalizedName.ShouldBe("szafran");
        (await db.Products.CountAsync(p => p.NormalizedName == "maka", TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Import_NewTagsAndUtensilsAreCreated_QuickTagIsIgnoredAndRecomputed()
    {
        var file = await ExportAsync(_ania, (await CreateRecipeWithFlourAsync()).GetProperty("id").GetGuid());
        var recipe = file["recipe"]!;
        recipe["tags"] = new JsonArray("OBIAD", "Na imprezę", "szybkie");
        recipe["utensils"] = new JsonArray("Patelnia", "Blender");
        recipe["prepMinutes"] = 5;
        recipe["cookMinutes"] = 10;

        var imported = await ImportOkAsync(_carol, file);

        imported.GetProperty("tags").EnumerateArray().Select(t => t.GetProperty("name").GetString()).Order().ShouldBe(["Na imprezę", "obiad", "szybkie"]);
        imported.GetProperty("utensils").EnumerateArray().Select(u => u.GetProperty("name").GetString()).Order().ShouldBe(["Blender", "patelnia"]);
        await using var db = _factory.CreateDbContext();
        (await db.Tags.CountAsync(t => t.NormalizedName == "obiad", TestContext.Current.CancellationToken)).ShouldBe(1);
        (await db.Tags.CountAsync(t => t.NormalizedName == "szybkie", TestContext.Current.CancellationToken)).ShouldBe(1);
        (await db.Tags.CountAsync(t => t.NormalizedName == "na imprez", TestContext.Current.CancellationToken)).ShouldBe(0);
        (await db.Tags.CountAsync(t => t.NormalizedName == "na impreze", TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Fact]
    public async Task Import_VariantsWithTheSameNameFromDifferentAuthors_GetUniqueNames()
    {
        var recipeId = await CreateRichRecipeWithTwoVariantsAsync();
        var file = await ExportAsync(_ania, recipeId);
        var variants = file["variants"]!.AsArray();
        variants[1]!["name"] = variants[0]!["name"]!.GetValue<string>();

        var imported = await ImportOkAsync(_carol, file);

        var list = await _carol.GetAsync($"/api/v1/recipes/{imported.GetProperty("id").GetGuid()}/variants");
        var names = (await ApiClient.ReadJsonAsync(list)).EnumerateArray().Select(v => v.GetProperty("name").GetString()).ToList();
        names.ShouldBe(["Bez masła", "Bez masła (2)"]);
    }

    [Fact]
    public async Task Import_BrokenOpReference_Returns400InvalidFormatAndPersistsNothing()
    {
        var recipeId = await CreateRichRecipeWithTwoVariantsAsync();
        var file = await ExportAsync(_ania, recipeId);
        file["variants"]![0]!["ingredientOps"]![0]!["targetKey"] = "i404";
        await using var before = _factory.CreateDbContext();
        var recipes = await before.Recipes.CountAsync(TestContext.Current.CancellationToken);

        var response = await _carol.PostJsonAsync("/api/v1/recipes/import", file);

        var problem = await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "import.invalid_format");
        problem.GetProperty("errors").GetProperty("variants[0].ingredientOps[0].targetKey")[0].GetString().ShouldBe("unknown_key");
        await AssertNothingPersistedAsync(recipes);
    }

    [Fact]
    public async Task Import_FailureInsideTheTransaction_RollsBackCatalogEntriesAndPhotos()
    {
        var recipeId = await CreateRichRecipeWithTwoVariantsAsync();
        var file = await ExportAsync(_ania, recipeId);

        // A product with no way to turn grams into pieces: the recipe is rejected after photos and catalog rows exist.
        file["products"]!.AsArray().Add(JsonNode.Parse("""{"key":"p9","name":"Szafran","category":"spices","measureKind":"count","displayClass":"piece","hasSizes":false,"allowHalf":false,"gramsPerMl":null,"gramsPerPiece":null,"defaultPackageAmount":null,"pieceForms":null}"""));
        file["recipe"]!["ingredients"]!.AsArray().Add(JsonNode.Parse("""{"key":"i9","productKey":"p9","quantity":{"value":5,"unit":"g"},"note":null,"optional":false}"""));
        file["recipe"]!["tags"] = new JsonArray("obiad", "nowy tag");
        await using var before = _factory.CreateDbContext();
        var recipes = await before.Recipes.CountAsync(TestContext.Current.CancellationToken);
        var tags = await before.Tags.CountAsync(TestContext.Current.CancellationToken);

        var response = await _carol.PostJsonAsync("/api/v1/recipes/import", file);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "import.invalid_format");
        await AssertNothingPersistedAsync(recipes);
        await using var db = _factory.CreateDbContext();
        (await db.Tags.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(tags);
        (await db.Products.AnyAsync(p => p.Name == "Szafran", TestContext.Current.CancellationToken)).ShouldBeFalse();
    }

    [Fact]
    public async Task Import_FormatVersion2_Returns400UnsupportedVersion()
    {
        var file = await ExportAsync(_ania, (await CreateRecipeWithFlourAsync()).GetProperty("id").GetGuid());
        file["formatVersion"] = 2;
        file["somethingNew"] = new JsonObject();

        var response = await _carol.PostJsonAsync("/api/v1/recipes/import", file);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "import.unsupported_version");
    }

    [Theory]
    [InlineData("""{"format":"something-else","formatVersion":1}""")]
    [InlineData("""{"format":"recipes-list/recipe","formatVersion":1,"exportedAt":"2026-10-24T10:00:00Z"}""")]
    [InlineData("""[1,2,3]""")]
    [InlineData("""not json at all""")]
    public async Task Import_MalformedFile_Returns400InvalidFormat(string body)
    {
        var response = await _carol.Http.PostAsync(
            "/api/v1/recipes/import", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "import.invalid_format");
    }

    [Fact]
    public async Task Import_PhotoThatIsNotAnImage_Returns400InvalidFormat()
    {
        var recipeId = await CreateRichRecipeWithTwoVariantsAsync();
        var file = await ExportAsync(_ania, recipeId);
        file["images"]!["img1"]!["dataBase64"] = Convert.ToBase64String("definitely not a picture"u8);

        var response = await _carol.PostJsonAsync("/api/v1/recipes/import", file);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "import.invalid_format");
        await using var db = _factory.CreateDbContext();
        (await db.Images.CountAsync(i => i.OwnerId == _carol.UserId, TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Import_FileOver25MB_Returns413ImportTooLarge()
    {
        var body = new string(' ', (int)ExportFormat.MaxFileBytes + 1);

        var response = await _carol.Http.PostAsync(
            "/api/v1/recipes/import", new StringContent(body, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.RequestEntityTooLarge, "import.too_large");
    }

    [Fact]
    public async Task Import_IsRateLimitedPerUser()
    {
        using var limited = _factory.WithWebHostBuilder(b => b.UseSetting("RateLimiting:ImportPerMinute", "1"));
        using var client = new ApiClient(limited.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        }));
        await client.LoginAsync("dorota");

        var first = await client.PostJsonAsync("/api/v1/recipes/import", new { });
        var second = await client.PostJsonAsync("/api/v1/recipes/import", new { });

        first.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await ContractAssert.IsProblemAsync(second, HttpStatusCode.TooManyRequests, "rate_limited");
    }

    [Fact]
    public async Task SampleFile_MatchesTheContractAndImports()
    {
        var path = Path.Combine(FindRepositoryRoot(), "shared", "samples", "export-v1.json");
        var text = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        ContractAssert.MatchesSchema(JsonDocument.Parse(text).RootElement, "RecipeExport");

        var response = await _carol.Http.PostAsync(
            "/api/v1/recipes/import", new StringContent(text, Encoding.UTF8, "application/json"), TestContext.Current.CancellationToken);

        var recipe = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "RecipeDetail");
        recipe.GetProperty("variantCount").GetInt32().ShouldBeGreaterThan(0);
    }

    private async Task AssertNothingPersistedAsync(int recipesBefore)
    {
        await using var db = _factory.CreateDbContext();
        (await db.Recipes.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(recipesBefore);
        (await db.Images.CountAsync(i => i.OwnerId == _carol.UserId, TestContext.Current.CancellationToken)).ShouldBe(0);
        (await db.Variants.CountAsync(v => v.AuthorId == _carol.UserId, TestContext.Current.CancellationToken)).ShouldBe(0);
        var carolsFiles = Directory.Exists(_imagesPath)
            ? Directory.GetFiles(_imagesPath, "*", SearchOption.AllDirectories).Length
            : 0;
        var knownImages = await db.Images.CountAsync(TestContext.Current.CancellationToken);
        carolsFiles.ShouldBeLessThanOrEqualTo(knownImages * 2, "rolled-back photos must be deleted from storage");
    }

    // A recipe with a cover, a photo in a prep step, two instruction steps, a tag, a utensil and times, plus two variants:
    // Bartek removes the butter, adds sugar after the flour and replaces a step with a photo; Ania lowers the flour.
    private async Task<Guid> CreateRichRecipeWithTwoVariantsAsync()
    {
        var cover = await UploadAsync(_ania, 120, 80);
        var stepPhoto = await UploadAsync(_ania, 90, 90);
        var body = Draft("Naleśniki z masłem", "public");
        body["description"] = "Prosty obiad";
        body["coverImageId"] = cover;
        body["prepMinutes"] = 10;
        body["cookMinutes"] = 15;
        body["tagIds"] = new[] { _dinner };
        body["utensilIds"] = new[] { _pan };
        body["ingredients"] = new object[]
        {
            Ingredient(_flour, 200, "g"),
            Ingredient(_butter, 50, "g", note: "miękkie"),
            Ingredient(_onion, 1, "piece"),
        };
        body["prepSteps"] = new object[]
        {
            new { id = (Guid?)null, blocks = new object[] { new { type = "text", text = "Przygotuj składniki." }, new { type = "image", imageId = stepPhoto, caption = "Tak ma wyglądać" } } },
        };
        body["instructionSteps"] = new object[] { Step("Wymieszaj."), Step("Smaż po minucie z każdej strony.") };
        var recipe = await CreateAsync(_ania, body);
        var id = recipe.GetProperty("id").GetGuid();

        var variantPhoto = await UploadAsync(_bartek, 60, 40);
        await PostVariantAsync(_bartek, id, "Bez masła", recipe, visibility: "public", change: copy =>
        {
            var ingredients = (List<Dictionary<string, object?>>)copy["ingredients"]!;
            ingredients.RemoveAt(1);
            ingredients.Insert(1, new Dictionary<string, object?> { ["id"] = null, ["productId"] = _sugar, ["quantity"] = new { value = 20, unit = "g" }, ["note"] = null, ["optional"] = false });
            var steps = (List<Dictionary<string, object?>>)copy["instructionSteps"]!;
            steps[1] = new Dictionary<string, object?>
            {
                ["id"] = steps[1]["id"],
                ["blocks"] = new object[] { new { type = "text", text = "Smaż na złoto." }, new { type = "image", imageId = variantPhoto, caption = (string?)null } },
            };
            copy["cookMinutes"] = 20;
        });
        await PostVariantAsync(_ania, id, "Mniej mąki", recipe, visibility: "public", change: copy =>
        {
            var ingredients = (List<Dictionary<string, object?>>)copy["ingredients"]!;
            ingredients[0]["quantity"] = new { value = 150, unit = "g" };
        });
        return id;
    }

    private async Task<JsonElement> CreateRecipeWithFlourAsync()
    {
        var body = Draft("Placki", "private");
        body["description"] = null;
        return await CreateAsync(_ania, body);
    }

    private async Task<JsonElement> CreateRecipeAsync(ApiClient client, string title, string visibility) =>
        await CreateAsync(client, Draft(title, visibility));

    private Dictionary<string, object?> Draft(string title, string visibility) => new()
    {
        ["title"] = title,
        ["description"] = null,
        ["coverImageId"] = null,
        ["prepMinutes"] = null,
        ["cookMinutes"] = null,
        ["tagIds"] = Array.Empty<Guid>(),
        ["utensilIds"] = Array.Empty<Guid>(),
        ["ingredients"] = new object[] { Ingredient(_flour, 200, "g") },
        ["prepSteps"] = Array.Empty<object>(),
        ["instructionSteps"] = new object[] { Step("Smaż.") },
        ["visibility"] = visibility,
    };

    private static object Ingredient(Guid productId, decimal value, string unit, string? note = null) => new
    {
        id = (Guid?)null,
        productId,
        quantity = new { value, unit },
        note,
        optional = false,
    };

    private static object Step(string text) => new { id = (Guid?)null, blocks = new object[] { new { type = "text", text } } };

    private static async Task<JsonElement> CreateAsync(ApiClient client, Dictionary<string, object?> body)
    {
        var response = await client.PostJsonAsync("/api/v1/recipes", body);
        return await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "RecipeDetail");
    }

    private static async Task<JsonElement> GetDetailAsync(ApiClient client, Guid id) =>
        await ContractAssert.IsJsonAsync(await client.GetAsync($"/api/v1/recipes/{id}"), HttpStatusCode.OK, "RecipeDetail");

    // POST /recipes/{id}/variants with the recipe as the editor would send it, after `change` edited the copy.
    private static async Task PostVariantAsync(
        ApiClient client, Guid recipeId, string name, JsonElement recipe, string visibility, Action<Dictionary<string, object?>> change)
    {
        var copy = new Dictionary<string, object?>
        {
            ["prepMinutes"] = recipe.GetProperty("prepMinutes"),
            ["cookMinutes"] = recipe.GetProperty("cookMinutes"),
            ["utensilIds"] = recipe.GetProperty("utensils").EnumerateArray().Select(u => u.GetProperty("id")).ToList(),
            ["ingredients"] = recipe.GetProperty("ingredients").EnumerateArray().Select(i => new Dictionary<string, object?>
            {
                ["id"] = i.GetProperty("id"),
                ["productId"] = i.GetProperty("product").GetProperty("id"),
                ["quantity"] = i.GetProperty("quantity"),
                ["note"] = i.GetProperty("note"),
                ["optional"] = i.GetProperty("optional"),
            }).ToList(),
            ["prepSteps"] = Steps(recipe, "prepSteps"),
            ["instructionSteps"] = Steps(recipe, "instructionSteps"),
        };
        change(copy);
        var response = await client.PostJsonAsync($"/api/v1/recipes/{recipeId}/variants", new { name, description = (string?)null, visibility, recipe = copy });
        await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantDetail");
    }

    private static List<Dictionary<string, object?>> Steps(JsonElement recipe, string name) =>
        [.. recipe.GetProperty(name).EnumerateArray().Select(s => new Dictionary<string, object?>
        {
            ["id"] = s.GetProperty("id"),
            ["blocks"] = s.GetProperty("blocks"),
        })];

    private static async Task<Guid> UploadAsync(ApiClient client, int width, int height)
    {
        var file = new ByteArrayContent(ImageFixtures.Png(width, height));
        file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        var content = new MultipartFormDataContent { { file, "file", "photo.png" } };
        var response = await client.Http.PostAsync("/api/v1/images", content, TestContext.Current.CancellationToken);
        var info = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "ImageInfo");
        return info.GetProperty("id").GetGuid();
    }

    private static async Task<JsonNode> ExportAsync(ApiClient client, Guid recipeId)
    {
        var response = await client.GetAsync($"/api/v1/recipes/{recipeId}/export");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<JsonElement> ImportOkAsync(ApiClient client, JsonNode file)
    {
        var response = await client.PostJsonAsync("/api/v1/recipes/import", file);
        return await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "RecipeDetail");
    }

    // Drops the timestamp and replaces each photo by its size, because import re-encodes photos.
    private static JsonNode Normalize(JsonNode export)
    {
        var copy = JsonNode.Parse(export.ToJsonString())!;
        copy.AsObject().Remove("exportedAt");
        foreach (var (key, image) in copy["images"]!.AsObject().ToList())
        {
            var (width, height) = ImageFixtures.SizeOf(Convert.FromBase64String(image!["dataBase64"]!.GetValue<string>()));
            copy["images"]![key] = $"{width}x{height}";
        }

        return copy;
    }

    private static Guid AddProduct(RecipesList.Infrastructure.Persistence.AppDbContext db, string name, MeasureKind kind, DisplayClass display, decimal? gramsPerPiece = null, bool hasSizes = false)
    {
        var product = new Product
        {
            Name = name,
            NormalizedName = NameNormalizer.Normalize(name),
            MeasureKind = kind,
            DisplayClass = display,
            GramsPerPiece = gramsPerPiece,
            HasSizes = hasSizes,
            CreatedAt = TestData.Now,
        };
        db.Products.Add(product);
        return product.Id;
    }

    private ApiClient NewClient() =>
        new(_web.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true,
        }));

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "api", "openapi.yaml")))
            {
                return dir.FullName;
            }
        }

        throw new FileNotFoundException("Repository root not found above " + AppContext.BaseDirectory);
    }
}
