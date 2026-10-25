using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;
using RecipesList.IntegrationTests.Variants;

namespace RecipesList.IntegrationTests.CookCounts;

/// <summary>D-28: cooking never touches the versioned recipe and variant rows, so editors do not hit 409.</summary>
public sealed class CookCountTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly VariantScenario _s = new();
    private ApiClient _ania = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _ania = factory.CreateApiClient();
        await _ania.LoginAsync("ania");
        await using var db = factory.CreateDbContext();
        await _s.SeedCatalogAsync(db);
    }

    public ValueTask DisposeAsync()
    {
        _ania.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Cook_ThenSaveTheRecipeWithTheVersionReadBefore_Returns200()
    {
        var recipe = await ArrangeRecipeAsync();
        var before = await GetAsync($"/api/v1/recipes/{recipe.Id}");
        await CookAsync(recipe.Id, null);

        var response = await _ania.PutJsonAsync($"/api/v1/recipes/{recipe.Id}", RecipeUpdate(before));

        var after = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "RecipeDetail");
        after.GetProperty("cookCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Cook_ThenSaveTheVariantWithTheVersionReadBefore_Returns200()
    {
        var (recipe, variantId) = await ArrangeAsync();
        var before = await GetAsync($"/api/v1/variants/{variantId}");
        await CookAsync(recipe.Id, variantId);
        var copy = VariantScenario.CopyOf(recipe);
        copy.Ingredients.RemoveAt(1);

        var response = await _ania.PutJsonAsync(
            $"/api/v1/variants/{variantId}", VariantScenario.Write("Bez soli", copy, "private", before.GetProperty("version").GetUInt32()));

        var after = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "VariantDetail");
        after.GetProperty("cookCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Cook_BumpsNeitherVersion()
    {
        var (recipe, variantId) = await ArrangeAsync();
        var recipeBefore = await GetAsync($"/api/v1/recipes/{recipe.Id}");
        var variantBefore = await GetAsync($"/api/v1/variants/{variantId}");

        await CookAsync(recipe.Id, variantId);

        var recipeAfter = await GetAsync($"/api/v1/recipes/{recipe.Id}");
        var variantAfter = await GetAsync($"/api/v1/variants/{variantId}");
        recipeAfter.GetProperty("version").GetUInt32().ShouldBe(recipeBefore.GetProperty("version").GetUInt32());
        variantAfter.GetProperty("version").GetUInt32().ShouldBe(variantBefore.GetProperty("version").GetUInt32());
    }

    [Fact]
    public async Task Cook_CountsAppearInEveryEndpointThatReturnsThem()
    {
        var (recipe, variantId) = await ArrangeAsync();
        await CookAsync(recipe.Id, variantId);
        await CookAsync(recipe.Id, null);

        (await GetAsync($"/api/v1/recipes/{recipe.Id}")).GetProperty("cookCount").GetInt32().ShouldBe(2);
        (await GetAsync($"/api/v1/variants/{variantId}")).GetProperty("cookCount").GetInt32().ShouldBe(1);
        var list = await GetAsync($"/api/v1/recipes/{recipe.Id}/variants");
        list.EnumerateArray().ShouldHaveSingleItem().GetProperty("cookCount").GetInt32().ShouldBe(1);
        var mine = await GetAsync("/api/v1/variants?author=me");
        mine.GetProperty("items").EnumerateArray().ShouldHaveSingleItem().GetProperty("cookCount").GetInt32().ShouldBe(1);
        var resolved = await GetAsync($"/api/v1/variants/{variantId}/resolved");
        resolved.GetProperty("cookCount").GetInt32().ShouldBe(2);
        var matrix = await GetAsync($"/api/v1/recipes/{recipe.Id}/variant-matrix");
        matrix.GetProperty("variants").EnumerateArray().Select(c => c.GetProperty("cookCount").GetInt32()).ShouldBe([2, 1]);
    }

    [Fact]
    public async Task Deduct_WithAVariant_CountsForTheVariantNotForTheBase()
    {
        var (recipe, variantId) = await ArrangeAsync();

        await CookAsync(recipe.Id, variantId);

        var matrix = await GetAsync($"/api/v1/recipes/{recipe.Id}/variant-matrix");
        var counts = matrix.GetProperty("variants").EnumerateArray()
            .ToDictionary(c => c.GetProperty("id").GetString()!, c => c.GetProperty("cookCount").GetInt32());
        counts["base"].ShouldBe(0);
        counts[variantId.ToString()].ShouldBe(1);
        (await GetAsync($"/api/v1/recipes/{recipe.Id}")).GetProperty("cookCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task NeverCooked_ReportsZero()
    {
        var (recipe, variantId) = await ArrangeAsync();

        (await GetAsync($"/api/v1/recipes/{recipe.Id}")).GetProperty("cookCount").GetInt32().ShouldBe(0);
        (await GetAsync($"/api/v1/variants/{variantId}")).GetProperty("cookCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task DeletingTheVariantOrTheRecipe_RemovesTheCountRows()
    {
        var (recipe, variantId) = await ArrangeAsync();
        await CookAsync(recipe.Id, variantId);
        var ct = TestContext.Current.CancellationToken;

        (await _ania.DeleteAsync($"/api/v1/variants/{variantId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using (var db = factory.CreateDbContext())
        {
            (await db.VariantCookCounts.CountAsync(ct)).ShouldBe(0);
            (await db.RecipeCookCounts.CountAsync(ct)).ShouldBe(1);
        }

        (await _ania.DeleteAsync($"/api/v1/recipes/{recipe.Id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using (var db = factory.CreateDbContext())
        {
            (await db.RecipeCookCounts.CountAsync(ct)).ShouldBe(0);
        }
    }

    private async Task<Recipe> ArrangeRecipeAsync()
    {
        Recipe recipe;
        await using (var db = factory.CreateDbContext())
        {
            recipe = await _s.AddRecipeAsync(db, _ania.UserId, VisibilityLevel.Private);
        }

        var fridgeId = await FridgeIdAsync();
        var item = await _ania.PostJsonAsync(
            $"/api/v1/fridges/{fridgeId}/items", new { productId = _s.Chicken.Id, packageAmount = 1000m, remainingPercent = 100m, note = (string?)null });
        item.StatusCode.ShouldBe(HttpStatusCode.Created);
        return recipe;
    }

    private async Task<(Recipe Recipe, Guid VariantId)> ArrangeAsync()
    {
        var recipe = await ArrangeRecipeAsync();
        var copy = VariantScenario.CopyOf(recipe);
        copy.Ingredients.RemoveAt(2);
        var created = await _ania.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write("Krócej", copy, "private"));
        var variant = await ContractAssert.IsJsonAsync(created, HttpStatusCode.Created, "VariantDetail");
        return (recipe, variant.GetProperty("id").GetGuid());
    }

    private async Task<Guid> FridgeIdAsync() =>
        (await GetAsync("/api/v1/me")).GetProperty("settings").GetProperty("defaultFridgeId").GetGuid();

    private async Task CookAsync(Guid recipeId, Guid? variantId)
    {
        var response = await _ania.PostJsonAsync(
            $"/api/v1/fridges/{await FridgeIdAsync()}/deductions",
            new { recipeId, variantId, lines = new[] { new { productId = _s.Chicken.Id, amount = 10m } } });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    private async Task<JsonElement> GetAsync(string path)
    {
        var response = await _ania.GetAsync(path);
        var body = await ApiClient.ReadJsonAsync(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        return body;
    }

    private static Dictionary<string, object?> RecipeUpdate(JsonElement detail) => new()
    {
        ["title"] = detail.GetProperty("title").GetString(),
        ["description"] = detail.GetProperty("description"),
        ["coverImageId"] = null,
        ["prepMinutes"] = detail.GetProperty("prepMinutes"),
        ["cookMinutes"] = detail.GetProperty("cookMinutes"),
        ["tagIds"] = detail.GetProperty("tags").EnumerateArray().Where(t => !t.GetProperty("isSystem").GetBoolean()).Select(t => t.GetProperty("id")).ToArray(),
        ["utensilIds"] = detail.GetProperty("utensils").EnumerateArray().Select(u => u.GetProperty("id")).ToArray(),
        ["ingredients"] = detail.GetProperty("ingredients").EnumerateArray().Select(i => (object)new
        {
            id = i.GetProperty("id"),
            productId = i.GetProperty("product").GetProperty("id"),
            quantity = i.GetProperty("quantity"),
            note = i.GetProperty("note"),
            optional = i.GetProperty("optional"),
        }).ToArray(),
        ["prepSteps"] = Steps(detail, "prepSteps"),
        ["instructionSteps"] = Steps(detail, "instructionSteps"),
        ["version"] = detail.GetProperty("version").GetUInt32(),
        ["deleteVariants"] = false,
    };

    private static object[] Steps(JsonElement detail, string name) =>
        [.. detail.GetProperty(name).EnumerateArray().Select(s => (object)new { id = s.GetProperty("id"), blocks = s.GetProperty("blocks") })];
}
