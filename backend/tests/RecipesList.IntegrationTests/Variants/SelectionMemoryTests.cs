using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Variants;

/// <summary>API.md §5.2: remembered picks and the product preferences derived from them (PROJECT §6.6).</summary>
public sealed class SelectionMemoryTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly VariantScenario _s = new();
    private ApiClient _ania = null!;
    private ApiClient _bartek = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _ania = factory.CreateApiClient();
        _bartek = factory.CreateApiClient();
        await _ania.LoginAsync("ania");
        await _bartek.LoginAsync("bartek");
        await using var db = factory.CreateDbContext();
        await _s.SeedCatalogAsync(db);
    }

    public ValueTask DisposeAsync()
    {
        _ania.Dispose();
        _bartek.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Get_NothingRemembered_Returns204_InvisibleRecipe_Returns404()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var hidden = await AddRecipeAsync(VisibilityLevel.Private, "Ukryty");

        (await _bartek.GetAsync($"/api/v1/recipes/{recipe.Id}/my-selection")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.IsProblemAsync(await _bartek.GetAsync($"/api/v1/recipes/{hidden.Id}/my-selection"), HttpStatusCode.NotFound, "recipe.not_found");
        await ContractAssert.IsProblemAsync(
            await _bartek.PutJsonAsync($"/api/v1/recipes/{hidden.Id}/my-selection", Selection()),
            HttpStatusCode.NotFound,
            "recipe.not_found");
    }

    [Fact]
    public async Task Put_SwappedProduct_PreferenceWeightCountsRecipes()
    {
        var first = await AddRecipeAsync(VisibilityLevel.Public, "Pierwszy");
        var second = await AddRecipeAsync(VisibilityLevel.Public, "Drugi");
        await CreateCreamSwapAsync(first);
        await CreateCreamSwapAsync(second);

        var saved = await ContractAssert.IsJsonAsync(
            await _bartek.PutJsonAsync($"/api/v1/recipes/{first.Id}/my-selection", Selection((CreamSlot(first), HeavyCreamOption, "user"))),
            HttpStatusCode.OK,
            "Selection");
        saved.GetProperty("picks").GetProperty(CreamSlot(first)).GetProperty("optionKey").GetString().ShouldBe(HeavyCreamOption);
        (await WeightAsync()).ShouldBe(1);

        await _bartek.PutJsonAsync($"/api/v1/recipes/{second.Id}/my-selection", Selection((CreamSlot(second), HeavyCreamOption, "user")));
        (await WeightAsync()).ShouldBe(2);

        // An auto pick is only a soft preference: it does not count.
        await _bartek.PutJsonAsync($"/api/v1/recipes/{second.Id}/my-selection", Selection((CreamSlot(second), HeavyCreamOption, "auto")));
        (await WeightAsync()).ShouldBe(1);

        await _bartek.PutJsonAsync($"/api/v1/recipes/{first.Id}/my-selection", Selection());
        (await WeightAsync()).ShouldBeNull();

        var remembered = await ContractAssert.IsJsonAsync(await _bartek.GetAsync($"/api/v1/recipes/{second.Id}/my-selection"), HttpStatusCode.OK, "Selection");
        remembered.GetProperty("picks").GetProperty(CreamSlot(second)).GetProperty("source").GetString().ShouldBe("auto");
    }

    [Fact]
    public async Task Put_PickOfTheBaseProduct_IsNotASwap()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);

        await _bartek.PutJsonAsync($"/api/v1/recipes/{recipe.Id}/my-selection", Selection((CreamSlot(recipe), $"{_s.Cream.Id}:200", "user")));

        await using var db = factory.CreateDbContext();
        (await db.ProductPreferences.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task Put_MalformedSlotKey_Returns400UnknownSlot()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);

        var response = await _bartek.PutJsonAsync($"/api/v1/recipes/{recipe.Id}/my-selection", Selection(("cream", "none", "user")));

        var problem = await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "selection.unknown_slot");
        problem.GetProperty("errors").TryGetProperty("picks.cream", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Put_SlotsAndOptionsThatNoLongerExist_AreDropped()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var chickenSlot = $"i:{recipe.Ingredients[0].Id}";
        var body = new
        {
            picks = new Dictionary<string, object>
            {
                [chickenSlot] = new { optionKey = $"{_s.Chicken.Id}:600", source = "user" },
                [$"p:{_s.Pepper.Id}"] = new { optionKey = $"{_s.Pepper.Id}:1", source = "user" },
                [CreamSlot(recipe)] = new { optionKey = HeavyCreamOption, source = "user" },
            },
            scaling = new { anchorSlotKey = chickenSlot, anchorBaseAmount = 300 },
        };

        var saved = await ContractAssert.IsJsonAsync(await _bartek.PutJsonAsync($"/api/v1/recipes/{recipe.Id}/my-selection", body), HttpStatusCode.OK, "Selection");

        saved.GetProperty("picks").EnumerateObject().Select(p => p.Name).ShouldBe([chickenSlot]);
        saved.GetProperty("scaling").GetProperty("anchorBaseAmount").GetDecimal().ShouldBe(300m);

        var lostAnchor = new { picks = new Dictionary<string, object>(), scaling = new { anchorSlotKey = $"i:{Guid.CreateVersion7()}", anchorBaseAmount = 1 } };
        var cleared = await ApiClient.ReadJsonAsync(await _bartek.PutJsonAsync($"/api/v1/recipes/{recipe.Id}/my-selection", lostAnchor));
        cleared.GetProperty("scaling").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task DeleteVariant_RemovesPicksOfItsOptionsFromEverySelection()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var variantId = await CreateCreamSwapAsync(recipe);
        var chickenSlot = $"i:{recipe.Ingredients[0].Id}";
        await _ania.PutJsonAsync(
            $"/api/v1/recipes/{recipe.Id}/my-selection",
            Selection((CreamSlot(recipe), HeavyCreamOption, "user"), (chickenSlot, $"{_s.Chicken.Id}:600", "auto")));
        (await WeightAsync(_ania.UserId)).ShouldBe(1);

        (await _bartek.DeleteAsync($"/api/v1/variants/{variantId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var selection = await ApiClient.ReadJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}/my-selection"));
        selection.GetProperty("picks").EnumerateObject().Select(p => p.Name).ShouldBe([chickenSlot]);
        (await WeightAsync(_ania.UserId)).ShouldBeNull();
    }

    [Fact]
    public async Task UpdateVariant_DropsPicksOfOptionsItNoLongerOffers()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var variantId = await CreateCreamSwapAsync(recipe);
        await _ania.PutJsonAsync($"/api/v1/recipes/{recipe.Id}/my-selection", Selection((CreamSlot(recipe), HeavyCreamOption, "user")));
        var detail = await ApiClient.ReadJsonAsync(await _bartek.GetAsync($"/api/v1/variants/{variantId}"));
        var copy = VariantScenario.CopyOf(recipe);
        copy.CookMinutes = 5;

        var response = await _bartek.PutJsonAsync($"/api/v1/variants/{variantId}", VariantScenario.Write("Śmietanka", copy, "public", detail.GetProperty("version").GetUInt32()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var selection = await ApiClient.ReadJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}/my-selection"));
        selection.GetProperty("picks").EnumerateObject().ShouldBeEmpty();
    }

    private string HeavyCreamOption => $"{_s.HeavyCream.Id}:200";

    private static string CreamSlot(Recipe recipe) => $"i:{recipe.Ingredients[1].Id}";

    private static object Selection(params (string Slot, string Option, string Source)[] picks) => new
    {
        picks = picks.ToDictionary(p => p.Slot, p => (object)new { optionKey = p.Option, source = p.Source }),
        scaling = (object?)null,
    };

    private async Task<Recipe> AddRecipeAsync(VisibilityLevel visibility, string title = "Kurczak w śmietanie")
    {
        await using var db = factory.CreateDbContext();
        return await _s.AddRecipeAsync(db, _ania.UserId, visibility, title);
    }

    private async Task<Guid> CreateCreamSwapAsync(Recipe recipe)
    {
        var copy = VariantScenario.CopyOf(recipe);
        copy.Ingredients[1]["productId"] = _s.HeavyCream.Id;
        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write("Śmietanka", copy));
        return (await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantDetail")).GetProperty("id").GetGuid();
    }

    private async Task<int?> WeightAsync(Guid? userId = null)
    {
        var user = userId ?? _bartek.UserId;
        await using var db = factory.CreateDbContext();
        var preference = await db.ProductPreferences.SingleOrDefaultAsync(
            p => p.UserId == user && p.FromProductId == _s.Cream.Id && p.ToProductId == _s.HeavyCream.Id,
            TestContext.Current.CancellationToken);
        return preference?.Weight;
    }
}
