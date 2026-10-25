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
        var clock = factory.UseFakeClock();
        await CookAsync(recipe.Id, variantId);
        clock.Advance(TimeSpan.FromHours(13));
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
        matrix.GetProperty("variants").EnumerateArray().Select(c => c.GetProperty("cookCount").GetInt32()).ShouldBe([1, 1]);
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

    [Fact]
    public async Task Deduct_WithoutAVariant_CountsForTheBaseAndTheTotal()
    {
        var (recipe, variantId) = await ArrangeAsync();

        var result = await CookAsync(recipe.Id, null);

        result.GetProperty("cookCounted").GetBoolean().ShouldBeTrue();
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 1, Base: 1, Variant: 0));
    }

    [Fact]
    public async Task Deduct_WithAVariant_CountsForTheVariantAndTheTotalOnly()
    {
        var (recipe, variantId) = await ArrangeAsync();

        var result = await CookAsync(recipe.Id, variantId);

        result.GetProperty("cookCounted").GetBoolean().ShouldBeTrue();
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 1, Base: 0, Variant: 1));
    }

    [Fact]
    public async Task Deduct_SecondTimeWithinTwelveHours_StillDeductsButDoesNotCount()
    {
        var (recipe, variantId) = await ArrangeAsync();
        var clock = factory.UseFakeClock();
        await CookAsync(recipe.Id, variantId);
        clock.Advance(TimeSpan.FromHours(11) + TimeSpan.FromMinutes(59));

        var second = await CookAsync(recipe.Id, null);

        second.GetProperty("cookCounted").GetBoolean().ShouldBeFalse();
        second.GetProperty("lines")[0].GetProperty("deducted").GetDecimal().ShouldBe(10m);
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 1, Base: 0, Variant: 1));
    }

    [Fact]
    public async Task Deduct_AfterTwelveHours_CountsAgain()
    {
        var (recipe, variantId) = await ArrangeAsync();
        var clock = factory.UseFakeClock();
        await CookAsync(recipe.Id, variantId);
        clock.Advance(TimeSpan.FromHours(12) + TimeSpan.FromSeconds(1));

        var second = await CookAsync(recipe.Id, null);

        second.GetProperty("cookCounted").GetBoolean().ShouldBeTrue();
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 2, Base: 1, Variant: 1));
    }

    [Fact]
    public async Task Deduct_WithoutARecipe_DoesNotCount()
    {
        await ArrangeRecipeAsync();

        var response = await _ania.PostJsonAsync(
            $"/api/v1/fridges/{await FridgeIdAsync()}/deductions",
            new { lines = new[] { new { productId = _s.Chicken.Id, amount = 10m } } });

        var result = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "DeductionResult");
        result.GetProperty("cookCounted").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Undo_ChangesNeitherTheCountsNorTheTimestamp()
    {
        var (recipe, variantId) = await ArrangeAsync();
        var clock = factory.UseFakeClock();
        var deduction = await CookAsync(recipe.Id, variantId);
        var before = await GetAsync($"/api/v1/recipes/{recipe.Id}");
        clock.Advance(TimeSpan.FromHours(1));

        var undo = await _ania.PostJsonAsync($"/api/v1/fridges/{await FridgeIdAsync()}/deductions/{deduction.GetProperty("id").GetGuid()}/undo", null);

        var undone = await ContractAssert.IsJsonAsync(undo, HttpStatusCode.OK, "DeductionResult");
        undone.GetProperty("cookCounted").GetBoolean().ShouldBeFalse();
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 1, Base: 0, Variant: 1));
        var after = await GetAsync($"/api/v1/recipes/{recipe.Id}");
        after.GetProperty("myLastCountedCookAt").GetDateTimeOffset().ShouldBe(before.GetProperty("myLastCountedCookAt").GetDateTimeOffset());
        await ContractAssert.IsProblemAsync(await PostCookedAsync(recipe.Id, null), HttpStatusCode.Conflict, "cook.too_soon");
    }

    [Fact]
    public async Task Cooked_CountsLikeADeductionAndThenIsBlockedFor12Hours()
    {
        var (recipe, variantId) = await ArrangeAsync();
        var clock = factory.UseFakeClock();
        var started = clock.GetUtcNow();

        var ok = await ContractAssert.IsJsonAsync(await PostCookedAsync(recipe.Id, variantId), HttpStatusCode.OK, "CookResult");
        ok.GetProperty("counted").GetBoolean().ShouldBeTrue();
        ok.GetProperty("recipeCookCount").GetInt32().ShouldBe(1);
        ok.GetProperty("variantCookCount").GetInt32().ShouldBe(1);
        ok.GetProperty("nextAllowedAt").GetDateTimeOffset().ShouldBe(started.AddHours(12));
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 1, Base: 0, Variant: 1));

        clock.Advance(TimeSpan.FromHours(5));
        var tooSoon = await ContractAssert.IsProblemAsync(await PostCookedAsync(recipe.Id, null), HttpStatusCode.Conflict, "cook.too_soon");
        tooSoon.GetProperty("nextAllowedAt").GetDateTimeOffset().ShouldBe(started.AddHours(12));
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 1, Base: 0, Variant: 1));

        clock.Advance(TimeSpan.FromHours(7));
        var again = await ContractAssert.IsJsonAsync(await PostCookedAsync(recipe.Id, null), HttpStatusCode.OK, "CookResult");
        again.GetProperty("recipeCookCount").GetInt32().ShouldBe(2);
        again.TryGetProperty("variantCookCount", out _).ShouldBeFalse();
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 2, Base: 1, Variant: 1));
    }

    [Fact]
    public async Task Cooked_WithoutABody_CountsTheBase()
    {
        var recipe = await ArrangeRecipeAsync();

        var response = await _ania.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/cooked", null);

        await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "CookResult");
        (await CountsAsync(recipe.Id, Guid.Empty)).ShouldBe((Total: 1, Base: 1, Variant: 0));
    }

    [Fact]
    public async Task Cooked_AfterADeduction_IsBlocked_AndTheOtherWayRound()
    {
        var (recipe, variantId) = await ArrangeAsync();
        var clock = factory.UseFakeClock();
        await CookAsync(recipe.Id, variantId);
        await ContractAssert.IsProblemAsync(await PostCookedAsync(recipe.Id, variantId), HttpStatusCode.Conflict, "cook.too_soon");

        clock.Advance(TimeSpan.FromHours(13));
        (await PostCookedAsync(recipe.Id, null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var deduction = await CookAsync(recipe.Id, null);
        deduction.GetProperty("cookCounted").GetBoolean().ShouldBeFalse();
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 2, Base: 1, Variant: 1));
    }

    [Fact]
    public async Task Cooked_SimultaneousRequests_CountExactlyOnce()
    {
        var (recipe, variantId) = await ArrangeAsync();

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => PostCookedAsync(recipe.Id, variantId)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(3);
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 1, Base: 0, Variant: 1));
    }

    [Fact]
    public async Task Cooked_ADeductionAndACookedRequestAtOnce_CountExactlyOnce()
    {
        var (recipe, variantId) = await ArrangeAsync();

        var cooked = PostCookedAsync(recipe.Id, variantId);
        var deducted = CookAsync(recipe.Id, variantId);
        await Task.WhenAll(cooked, deducted);

        (await CountsAsync(recipe.Id, variantId)).Total.ShouldBe(1);
    }

    [Fact]
    public async Task Cooked_AnotherUserIsNotLimitedByMine()
    {
        Recipe recipe;
        await using (var db = factory.CreateDbContext())
        {
            recipe = await _s.AddRecipeAsync(db, _ania.UserId, VisibilityLevel.Public);
        }

        using var bartek = factory.CreateApiClient();
        await bartek.LoginAsync("bartek");
        (await PostCookedAsync(recipe.Id, null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/cooked", new { })).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await CountsAsync(recipe.Id, Guid.Empty)).ShouldBe((Total: 2, Base: 2, Variant: 0));
        (await GetAsync($"/api/v1/recipes/{recipe.Id}")).GetProperty("myLastCountedCookAt").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [Fact]
    public async Task Cooked_ArchivedRecipe_Returns409()
    {
        var recipe = await ArrangeRecipeAsync();
        await using (var db = factory.CreateDbContext())
        {
            await db.Recipes.Where(r => r.Id == recipe.Id).ExecuteUpdateAsync(
                s => s.SetProperty(r => r.IsArchived, true), TestContext.Current.CancellationToken);
        }

        await ContractAssert.IsProblemAsync(await PostCookedAsync(recipe.Id, null), HttpStatusCode.Conflict, "recipe.archived");
    }

    [Fact]
    public async Task Cooked_InvisibleRecipe_Returns404()
    {
        var recipe = await ArrangeRecipeAsync();
        using var bartek = factory.CreateApiClient();
        await bartek.LoginAsync("bartek");

        var response = await bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/cooked", new { });

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "recipe.not_found");
        await ContractAssert.IsProblemAsync(await PostCookedAsync(Guid.NewGuid(), null), HttpStatusCode.NotFound, "recipe.not_found");
    }

    [Fact]
    public async Task Cooked_VariantOfAnotherRecipeOrUnknown_Returns404()
    {
        var (recipe, variantId) = await ArrangeAsync();
        Recipe other;
        await using (var db = factory.CreateDbContext())
        {
            other = await _s.AddRecipeAsync(db, _ania.UserId, VisibilityLevel.Private, "Inny przepis");
        }

        await ContractAssert.IsProblemAsync(await PostCookedAsync(other.Id, variantId), HttpStatusCode.NotFound, "variant.not_found");
        await ContractAssert.IsProblemAsync(await PostCookedAsync(recipe.Id, Guid.NewGuid()), HttpStatusCode.NotFound, "variant.not_found");
        (await CountsAsync(recipe.Id, variantId)).ShouldBe((Total: 0, Base: 0, Variant: 0));
    }

    [Fact]
    public async Task RecipeDetail_ReportsTheCallersLastCountedCook()
    {
        var recipe = await ArrangeRecipeAsync();
        var clock = factory.UseFakeClock();
        var detail = await ContractAssert.IsJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}"), HttpStatusCode.OK, "RecipeDetail");
        detail.GetProperty("myLastCountedCookAt").ValueKind.ShouldBe(JsonValueKind.Null);

        var cooked = clock.GetUtcNow();
        await CookAsync(recipe.Id, null);
        clock.Advance(TimeSpan.FromHours(2));
        await PostCookedAsync(recipe.Id, null);

        var after = await ContractAssert.IsJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}"), HttpStatusCode.OK, "RecipeDetail");
        after.GetProperty("myLastCountedCookAt").GetDateTimeOffset().ShouldBe(cooked);
    }

    private Task<HttpResponseMessage> PostCookedAsync(Guid recipeId, Guid? variantId) =>
        _ania.PostJsonAsync($"/api/v1/recipes/{recipeId}/cooked", new { variantId });

    private async Task<(int Total, int Base, int Variant)> CountsAsync(Guid recipeId, Guid variantId)
    {
        await using var db = factory.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;
        var recipe = await db.RecipeCookCounts.Where(c => c.RecipeId == recipeId).SingleOrDefaultAsync(ct);
        var variant = await db.VariantCookCounts.Where(c => c.VariantId == variantId).Select(c => c.Count).SingleOrDefaultAsync(ct);
        return (recipe?.Count ?? 0, recipe?.BaseCount ?? 0, variant);
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

    private async Task<JsonElement> CookAsync(Guid recipeId, Guid? variantId)
    {
        var response = await _ania.PostJsonAsync(
            $"/api/v1/fridges/{await FridgeIdAsync()}/deductions",
            new { recipeId, variantId, lines = new[] { new { productId = _s.Chicken.Id, amount = 10m } } });
        return await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "DeductionResult");
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
