using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
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

/// <summary>GET /recipes/import-matches and POST /recipes/{id}/variants/import (API.md §4; PROJECT §4.7, D-35).</summary>
public sealed class ImportAsVariantsTests : IClassFixture<ApiFactory>, IAsyncLifetime, IDisposable
{
    private const string Title = "Naleśniki z masłem";

    private readonly ApiFactory _factory;
    private readonly WebApplicationFactory<Program> _web;
    private readonly string _imagesPath = Path.Combine(Path.GetTempPath(), "recipes-list-tests", Guid.NewGuid().ToString("N"));

    private ApiClient _ania = null!;
    private ApiClient _bartek = null!;
    private ApiClient _carol = null!;
    private Guid _flour;
    private Guid _butter;
    private Guid _milk;
    private Guid _eggs;
    private Guid _pan;

    public ImportAsVariantsTests(ApiFactory factory)
    {
        _factory = factory;
        _web = factory.WithWebHostBuilder(b => b.UseSetting("Storage:ImagesPath", _imagesPath));
    }

    public async ValueTask InitializeAsync()
    {
        await _factory.ResetDatabaseAsync();
        _ania = ApiClients.Create(_web);
        _bartek = ApiClients.Create(_web);
        _carol = ApiClients.Create(_web);
        await _ania.LoginAsync("ania");
        await _bartek.LoginAsync("bartek");
        await _carol.LoginAsync("carol");

        await using var db = _factory.CreateDbContext();
        _flour = AddProduct(db, "Mąka");
        _butter = AddProduct(db, "Masło");
        _milk = AddProduct(db, "Mleko", MeasureKind.Volume, DisplayClass.Liquid);
        _eggs = AddProduct(db, "Jajka", MeasureKind.Count, DisplayClass.Piece);
        var pan = new Utensil { Name = "patelnia", NormalizedName = "patelnia" };
        var oven = new Utensil { Name = "piekarnik", NormalizedName = "piekarnik" };
        db.Utensils.AddRange(pan, oven);
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
    public async Task Matches_NormalizedTitleAmongVisibleNonArchived_OwnFirstThenNewest()
    {
        var bartekOld = await CreateRecipeAsync(_bartek, "  naleśniki Z MASŁEM ", "public");
        var bartekNew = await CreateRecipeAsync(_bartek, "Nalesniki z maslem", "public");
        var own = await CreateRecipeAsync(_ania, Title, "private");
        await CreateRecipeAsync(_carol, Title, "private");
        await CreateRecipeAsync(_bartek, "Naleśniki z dżemem", "public");
        var archived = await CreateRecipeAsync(_bartek, Title, "public");
        await PostVariantAsync(_ania, archived, "Mój", c => c["cookMinutes"] = 5);
        (await _bartek.DeleteAsync($"/api/v1/recipes/{archived}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await _ania.GetAsync($"/api/v1/recipes/import-matches?title={Uri.EscapeDataString("NALEŚNIKI   z masłem")}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ApiClient.ReadJsonAsync(response);
        foreach (var item in body.EnumerateArray())
        {
            ContractAssert.MatchesSchema(item, "RecipeSummary");
        }

        body.EnumerateArray().Select(r => r.GetProperty("id").GetGuid()).ShouldBe([own, bartekNew, bartekOld]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("%20%20")]
    public async Task Matches_WithoutTitle_Returns400(string title)
    {
        var response = await _ania.GetAsync($"/api/v1/recipes/import-matches?title={title}");

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Matches_TitleOver150Characters_Returns400()
    {
        var response = await _ania.GetAsync($"/api/v1/recipes/import-matches?title={new string('a', 151)}");

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task ReimportOwnExport_SkipsTheRecipeAndItsExistingVariants()
    {
        var recipe = await CreateRichRecipeAsync();
        var file = await ExportAsync(_ania, recipe);
        file["variants"]!.AsArray().Count.ShouldBe(2);

        var response = await _ania.PostJsonAsync($"/api/v1/recipes/{recipe}/variants/import", file);

        var body = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "VariantImportResult");
        body.GetProperty("recipeId").GetGuid().ShouldBe(recipe);
        body.GetProperty("variants").GetArrayLength().ShouldBe(0);
        body.GetProperty("skipped").GetInt32().ShouldBe(3);
        await using var db = _factory.CreateDbContext();
        (await db.Variants.CountAsync(v => v.RecipeId == recipe, TestContext.Current.CancellationToken)).ShouldBe(2);
        (await db.Images.CountAsync(i => i.OwnerId == _ania.UserId, TestContext.Current.CancellationToken)).ShouldBe(2, "no photo is stored twice");
    }

    [Fact]
    public async Task DifferentFile_BecomesOneVariantThatResolvesToTheFileRecipe()
    {
        var recipe = await CreateRichRecipeAsync();
        var file = await ExportAsync(_ania, recipe);
        file["variants"] = new JsonArray();
        var products = file["products"]!.AsArray();
        products.Add(JsonNode.Parse("""{"key":"p9","name":"Cukier wanilinowy","category":"sweets","measureKind":"mass","displayClass":"weight","hasSizes":false,"allowHalf":false,"gramsPerMl":null,"gramsPerPiece":null,"defaultPackageAmount":null,"pieceForms":null}"""));
        var ingredients = file["recipe"]!["ingredients"]!.AsArray();
        ingredients[0]!["quantity"]!["value"] = 250;                    // one changed amount
        ingredients.RemoveAt(1);                                         // one missing row (butter)
        ingredients.Insert(1, JsonNode.Parse("""{"key":"i9","productKey":"p9","quantity":{"value":16,"unit":"g"},"note":"do ciasta","optional":false}"""));
        var steps = file["recipe"]!["instructionSteps"]!.AsArray();
        steps[0]!["blocks"] = JsonNode.Parse("""[{"type":"text","text":"Wymieszaj dokładnie."}]""");
        steps.Add(JsonNode.Parse("""{"key":"s9","blocks":[{"type":"text","text":"Podawaj od razu."}]}"""));
        file["recipe"]!["utensils"] = new JsonArray("piekarnik");
        file["recipe"]!["cookMinutes"] = 25;

        var response = await _carol.PostJsonAsync($"/api/v1/recipes/{recipe}/variants/import", file);

        var body = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantImportResult");
        body.GetProperty("skipped").GetInt32().ShouldBe(0);
        var variant = body.GetProperty("variants").EnumerateArray().Single();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/variants/{variant.GetProperty("id").GetGuid()}");
        variant.GetProperty("name").GetString().ShouldBe("Wersja @ania");
        variant.GetProperty("visibility").GetString().ShouldBe("private");
        variant.GetProperty("author").GetProperty("nickname").GetString().ShouldBe("carol");
        variant.GetProperty("description").GetString().ShouldBe($"Prosty obiad\n\n{ImportFooter.Prefix}ania");

        var resolved = await ResolvedAsync(_carol, variant.GetProperty("id").GetGuid());
        Content(resolved).ShouldBe(FileContent(file));
        resolved.GetProperty("ingredients")[0].GetProperty("id").GetGuid()
            .ShouldBe((await DetailAsync(_ania, recipe)).GetProperty("ingredients")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task FileVariants_BecomeVariantsThatResolveLikeInTheFile()
    {
        var target = await CreateRecipeAsync(_ania, Title, "public");
        var other = await CreateRecipeAsync(_bartek, Title, "public", c =>
        {
            c["ingredients"] = new object[] { Ingredient(_milk, 500, "ml"), Ingredient(_flour, 300, "g"), Ingredient(_eggs, 2, "piece") };
            c["instructionSteps"] = new object[] { Step("Zmiksuj."), Step("Odstaw na 10 minut.") };
        });
        await PostVariantAsync(_bartek, other, "Bez jajek", copy =>
        {
            var rows = (List<Dictionary<string, object?>>)copy["ingredients"]!;
            rows.RemoveAt(2);
            rows.Insert(0, new Dictionary<string, object?> { ["id"] = null, ["productId"] = _butter, ["quantity"] = new { value = 30, unit = "g" }, ["note"] = "roztopione", ["optional"] = true });
            ((List<Dictionary<string, object?>>)copy["instructionSteps"]!).Add(new Dictionary<string, object?> { ["id"] = null, ["blocks"] = new object[] { new { type = "text", text = "Smaż." } } });
            copy["utensilIds"] = new[] { _pan };
            copy["prepMinutes"] = 7;
        }, visibility: "public");
        var file = await ExportAsync(_bartek, other);
        var expected = Content(await ResolvedAsync(_bartek, await SingleVariantIdAsync(_bartek, other)));

        var response = await _carol.PostJsonAsync($"/api/v1/recipes/{target}/variants/import", file);

        var body = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantImportResult");
        var created = body.GetProperty("variants").EnumerateArray().ToList();
        created.Select(v => v.GetProperty("name").GetString()).ShouldBe(["Wersja @bartek", "Bez jajek"]);
        created[1].GetProperty("description").GetString().ShouldBe($"{ImportFooter.Prefix}bartek");
        Content(await ResolvedAsync(_carol, created[1].GetProperty("id").GetGuid())).ShouldBe(expected);
        Content(await ResolvedAsync(_carol, created[0].GetProperty("id").GetGuid())).ShouldBe(Content(await DetailAsync(_bartek, other)));
    }

    [Fact]
    public async Task NewStepPhotos_AreStoredForTheImporter_TheCoverIsNot()
    {
        var target = await CreateRecipeAsync(_ania, Title, "public");
        var cover = await UploadAsync(_bartek, 120, 80);
        var photo = await UploadAsync(_bartek, 50, 50);
        var other = await CreateRecipeAsync(_bartek, Title, "public", c =>
        {
            c["coverImageId"] = cover;
            c["instructionSteps"] = new object[]
            {
                new { id = (Guid?)null, blocks = new object[] { new { type = "text", text = "Smaż." }, new { type = "image", imageId = photo, caption = "Gotowe" } } },
            };
        });
        var file = await ExportAsync(_bartek, other);

        var response = await _carol.PostJsonAsync($"/api/v1/recipes/{target}/variants/import", file);

        var body = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantImportResult");
        var resolved = await ResolvedAsync(_carol, body.GetProperty("variants")[0].GetProperty("id").GetGuid());
        var block = resolved.GetProperty("instructionSteps")[0].GetProperty("blocks")[1];
        block.GetProperty("caption").GetString().ShouldBe("Gotowe");
        var imageId = block.GetProperty("imageId").GetGuid();
        await using var db = _factory.CreateDbContext();
        var carols = await db.Images.Where(i => i.OwnerId == _carol.UserId).Select(i => i.Id).ToListAsync(TestContext.Current.CancellationToken);
        carols.ShouldBe([imageId]);
        (await _carol.GetAsync($"/api/v1/images/{imageId}?size=full")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NameClash_AmongTheImportersVariants_GetsANumber()
    {
        var recipe = await CreateRecipeAsync(_ania, Title, "public");
        await PostVariantAsync(_carol, recipe, "Wersja @ania", c => c["cookMinutes"] = 99);
        var file = await ExportAsync(_ania, recipe);
        file["recipe"]!["cookMinutes"] = 12;

        var response = await _carol.PostJsonAsync($"/api/v1/recipes/{recipe}/variants/import", file);

        var body = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantImportResult");
        body.GetProperty("variants")[0].GetProperty("name").GetString().ShouldBe("Wersja @ania (2)");
    }

    [Fact]
    public async Task ArchivedTarget_Returns409()
    {
        var recipe = await CreateRecipeAsync(_ania, Title, "public");
        await PostVariantAsync(_bartek, recipe, "Mój", c => c["cookMinutes"] = 5);
        var file = await ExportAsync(_ania, recipe);
        (await _ania.DeleteAsync($"/api/v1/recipes/{recipe}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe}/variants/import", file);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Conflict, "recipe.archived");
    }

    [Fact]
    public async Task TargetNotVisible_Returns404()
    {
        var recipe = await CreateRecipeAsync(_ania, Title, "private");
        var file = await ExportAsync(_ania, recipe);

        var response = await _carol.PostJsonAsync($"/api/v1/recipes/{recipe}/variants/import", file);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "recipe.not_found");
    }

    [Fact]
    public async Task InvalidFile_Returns400AndCreatesNothing()
    {
        var recipe = await CreateRecipeAsync(_ania, Title, "public");
        var file = await ExportAsync(_ania, recipe);
        file["recipe"]!["ingredients"]![0]!["productKey"] = "missing";

        var response = await _carol.PostJsonAsync($"/api/v1/recipes/{recipe}/variants/import", file);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "import.invalid_format");
        await using var db = _factory.CreateDbContext();
        (await db.Variants.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    // A public recipe with a cover, a photo in a prep step, two instruction steps, a utensil and times, plus two
    // variants: Bartek's (no butter, a photo step) and Ania's (less flour).
    private async Task<Guid> CreateRichRecipeAsync()
    {
        var cover = await UploadAsync(_ania, 120, 80);
        var stepPhoto = await UploadAsync(_ania, 90, 90);
        var recipe = await CreateRecipeAsync(_ania, Title, "public", body =>
        {
            body["description"] = "Prosty obiad";
            body["coverImageId"] = cover;
            body["prepMinutes"] = 10;
            body["cookMinutes"] = 15;
            body["utensilIds"] = new[] { _pan };
            body["ingredients"] = new object[] { Ingredient(_flour, 200, "g"), Ingredient(_butter, 50, "g", note: "miękkie"), Ingredient(_milk, 300, "ml") };
            body["prepSteps"] = new object[]
            {
                new { id = (Guid?)null, blocks = new object[] { new { type = "text", text = "Przygotuj składniki." }, new { type = "image", imageId = stepPhoto, caption = "Tak ma wyglądać" } } },
            };
            body["instructionSteps"] = new object[] { Step("Wymieszaj."), Step("Smaż po minucie z każdej strony.") };
        });

        var variantPhoto = await UploadAsync(_bartek, 60, 40);
        await PostVariantAsync(_bartek, recipe, "Bez masła", copy =>
        {
            ((List<Dictionary<string, object?>>)copy["ingredients"]!).RemoveAt(1);
            var steps = (List<Dictionary<string, object?>>)copy["instructionSteps"]!;
            steps[1] = new Dictionary<string, object?>
            {
                ["id"] = steps[1]["id"],
                ["blocks"] = new object[] { new { type = "text", text = "Smaż na złoto." }, new { type = "image", imageId = variantPhoto, caption = (string?)null } },
            };
        }, visibility: "public");
        await PostVariantAsync(_ania, recipe, "Mniej mąki", copy =>
            ((List<Dictionary<string, object?>>)copy["ingredients"]!)[0]["quantity"] = new { value = 150, unit = "g" }, visibility: "public");
        return recipe;
    }

    private async Task<Guid> CreateRecipeAsync(ApiClient client, string title, string visibility, Action<Dictionary<string, object?>>? change = null)
    {
        var body = new Dictionary<string, object?>
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
        change?.Invoke(body);
        var response = await client.PostJsonAsync("/api/v1/recipes", body);
        var recipe = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "RecipeDetail");
        return recipe.GetProperty("id").GetGuid();
    }

    private static object Ingredient(Guid productId, decimal value, string unit, string? note = null) =>
        new { id = (Guid?)null, productId, quantity = new { value, unit }, note, optional = false };

    private static object Step(string text) => new { id = (Guid?)null, blocks = new object[] { new { type = "text", text } } };

    // POST /recipes/{id}/variants with the recipe as the editor sends it, after `change` edited the copy.
    private static async Task PostVariantAsync(
        ApiClient client, Guid recipeId, string name, Action<Dictionary<string, object?>> change, string visibility = "private")
    {
        var recipe = await DetailAsync(client, recipeId);
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
            ["prepSteps"] = StepsOf(recipe, "prepSteps"),
            ["instructionSteps"] = StepsOf(recipe, "instructionSteps"),
        };
        change(copy);
        var response = await client.PostJsonAsync($"/api/v1/recipes/{recipeId}/variants", new { name, description = (string?)null, visibility, recipe = copy });
        await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantDetail");
    }

    private static List<Dictionary<string, object?>> StepsOf(JsonElement recipe, string name) =>
        [.. recipe.GetProperty(name).EnumerateArray().Select(s => new Dictionary<string, object?>
        {
            ["id"] = s.GetProperty("id"),
            ["blocks"] = s.GetProperty("blocks"),
        })];

    private static async Task<JsonElement> DetailAsync(ApiClient client, Guid id) =>
        await ContractAssert.IsJsonAsync(await client.GetAsync($"/api/v1/recipes/{id}"), HttpStatusCode.OK, "RecipeDetail");

    private static async Task<JsonElement> ResolvedAsync(ApiClient client, Guid variantId) =>
        await ContractAssert.IsJsonAsync(await client.GetAsync($"/api/v1/variants/{variantId}/resolved"), HttpStatusCode.OK, "ResolvedRecipe");

    private static async Task<Guid> SingleVariantIdAsync(ApiClient client, Guid recipeId)
    {
        var list = await ApiClient.ReadJsonAsync(await client.GetAsync($"/api/v1/recipes/{recipeId}/variants"));
        return list.EnumerateArray().Single().GetProperty("id").GetGuid();
    }

    // The content of a recipe view, without ids: ingredients (product name, quantity, note, optional), step texts,
    // utensil names (sorted) and times.
    private static string Content(JsonElement recipe)
    {
        var ingredients = recipe.GetProperty("ingredients").EnumerateArray().Select(i =>
            $"{i.GetProperty("product").GetProperty("name").GetString()}|{Quantity(i.GetProperty("quantity"))}|{Text(i.GetProperty("note"))}|{i.GetProperty("optional").GetBoolean()}");
        var utensils = recipe.GetProperty("utensils").EnumerateArray().Select(u => u.GetProperty("name").GetString()!).Order(StringComparer.Ordinal);
        return string.Join("\n", ingredients
            .Concat(StepTexts(recipe.GetProperty("prepSteps")).Select(s => "prep:" + s))
            .Concat(StepTexts(recipe.GetProperty("instructionSteps")).Select(s => "instr:" + s))
            .Append("utensils:" + string.Join(",", utensils))
            .Append($"times:{Text(recipe.GetProperty("prepMinutes"))}/{Text(recipe.GetProperty("cookMinutes"))}"));
    }

    // The same content read from an export file.
    private static string FileContent(JsonNode file)
    {
        var products = file["products"]!.AsArray().ToDictionary(p => p!["key"]!.GetValue<string>(), p => p!["name"]!.GetValue<string>());
        var recipe = file["recipe"]!;
        var ingredients = recipe["ingredients"]!.AsArray().Select(i =>
            $"{products[i!["productKey"]!.GetValue<string>()]}|{Quantity(JsonSerializer.SerializeToElement(i["quantity"]))}|{i["note"]?.GetValue<string>() ?? ""}|{i["optional"]!.GetValue<bool>()}");
        var utensils = recipe["utensils"]!.AsArray().Select(u => u!.GetValue<string>()).Order(StringComparer.Ordinal);
        return string.Join("\n", ingredients
            .Concat(StepTexts(JsonSerializer.SerializeToElement(recipe["prepSteps"])).Select(s => "prep:" + s))
            .Concat(StepTexts(JsonSerializer.SerializeToElement(recipe["instructionSteps"])).Select(s => "instr:" + s))
            .Append("utensils:" + string.Join(",", utensils))
            .Append($"times:{recipe["prepMinutes"]?.ToString() ?? ""}/{recipe["cookMinutes"]?.ToString() ?? ""}"));
    }

    private static IEnumerable<string> StepTexts(JsonElement steps) =>
        steps.EnumerateArray().Select(s => string.Join(" + ", s.GetProperty("blocks").EnumerateArray().Select(b =>
            b.GetProperty("type").GetString() == "text" ? b.GetProperty("text").GetString() : "[photo]")));

    private static string Quantity(JsonElement q) => q.ValueKind == JsonValueKind.Object
        ? $"{q.GetProperty("value").GetDecimal().ToString("G29", CultureInfo.InvariantCulture)} {q.GetProperty("unit").GetString()}"
        : "";

    private static string Text(JsonElement e) => e.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? "" : e.ToString();

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

    private static Guid AddProduct(
        RecipesList.Infrastructure.Persistence.AppDbContext db, string name, MeasureKind kind = MeasureKind.Mass, DisplayClass display = DisplayClass.Weight)
    {
        var product = new Product
        {
            Name = name,
            NormalizedName = NameNormalizer.Normalize(name),
            MeasureKind = kind,
            DisplayClass = display,
            CreatedAt = TestData.Now,
        };
        db.Products.Add(product);
        return product.Id;
    }
}
