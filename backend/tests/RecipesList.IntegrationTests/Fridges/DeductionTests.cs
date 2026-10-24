using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Fridges;

/// <summary>Deducting a cooked recipe and undoing it (API.md §7, PROJECT §9).</summary>
public sealed class DeductionTests(ApiFactory factory) : FridgeTestBase(factory)
{
    [Fact]
    public async Task Deduct_SeveralPackages_UsesMostUsedFirstAndReportsShortfall()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var full = await AddItemAsync(anna, fridgeId, Milk, 1000m, 100m);
        var low = await AddItemAsync(anna, fridgeId, Milk, 1000m, 20m);
        var cream = await AddItemAsync(anna, fridgeId, Cream, 330m, 50m);

        var result = await DeductAsync(anna, fridgeId, null, null, (Milk, 500m), (Cream, 200m), (Flour, 100m));

        result.GetProperty("undone").GetBoolean().ShouldBeFalse();
        var lines = result.GetProperty("lines").EnumerateArray().ToList();
        lines.Count.ShouldBe(3);

        var milk = lines[0];
        milk.GetProperty("deducted").GetDecimal().ShouldBe(500m);
        milk.GetProperty("shortfall").GetDecimal().ShouldBe(0m);
        milk.GetProperty("items").EnumerateArray()
            .Select(i => (i.GetProperty("itemId").GetGuid(), i.GetProperty("newPercent").GetDecimal(), i.GetProperty("removed").GetBoolean()))
            .ShouldBe([(Id(low), 0m, true), (Id(full), 70m, false)]);

        var creamLine = lines[1];
        creamLine.GetProperty("deducted").GetDecimal().ShouldBe(165m);
        creamLine.GetProperty("shortfall").GetDecimal().ShouldBe(35m);
        creamLine.GetProperty("items")[0].GetProperty("itemId").GetGuid().ShouldBe(Id(cream));
        creamLine.GetProperty("items")[0].GetProperty("removed").GetBoolean().ShouldBeTrue();

        lines[2].GetProperty("shortfall").GetDecimal().ShouldBe(100m);
        lines[2].GetProperty("items").GetArrayLength().ShouldBe(0);

        var items = await ItemsAsync(anna, fridgeId);
        items.EnumerateArray().ShouldHaveSingleItem().GetProperty("remainingPercent").GetDecimal().ShouldBe(70m);
    }

    [Fact]
    public async Task DeductThenUndo_RestoresTheExactPreviousState()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddItemAsync(anna, fridgeId, Milk, 1000m, 100m, "pełne");
        await AddItemAsync(anna, fridgeId, Milk, 1000m, 20m, "resztka");
        await AddItemAsync(anna, fridgeId, Eggs, 10m, 60m);
        var before = await ItemsAsync(anna, fridgeId);

        var deduction = await DeductAsync(anna, fridgeId, null, null, (Milk, 500m), (Eggs, 6m));
        (await ItemsAsync(anna, fridgeId)).GetArrayLength().ShouldBe(1);

        var response = await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions/{Id(deduction)}/undo", null);
        var undone = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "DeductionResult");

        undone.GetProperty("undone").GetBoolean().ShouldBeTrue();
        undone.GetProperty("id").GetGuid().ShouldBe(Id(deduction));
        var after = await ItemsAsync(anna, fridgeId);
        Comparable(after).ShouldBe(Comparable(before));

        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions/{Id(deduction)}/undo", null), HttpStatusCode.Conflict, "deduction.already_undone");
    }

    [Fact]
    public async Task DeductEditUndo_TouchedItemChanged_Returns409()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddItemAsync(anna, fridgeId, Milk, 1000m, 100m);
        var deduction = await DeductAsync(anna, fridgeId, null, null, (Milk, 300m));
        var item = (await ItemsAsync(anna, fridgeId))[0];
        (await anna.PutJsonAsync($"/api/v1/fridges/{fridgeId}/items/{Id(item)}", new
        {
            productId = Milk,
            packageAmount = 1000,
            remainingPercent = 50,
            note = (string?)null,
            version = item.GetProperty("version").GetUInt32(),
        })).StatusCode.ShouldBe(HttpStatusCode.OK);

        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions/{Id(deduction)}/undo", null), HttpStatusCode.Conflict, "deduction.items_changed");
        (await ItemsAsync(anna, fridgeId))[0].GetProperty("remainingPercent").GetDecimal().ShouldBe(50m);
    }

    [Fact]
    public async Task Undo_RemovedItemWasRecreatedUnderTheSameId_Returns409()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var item = await AddItemAsync(anna, fridgeId, Milk, 1000m, 10m);
        var deduction = await DeductAsync(anna, fridgeId, null, null, (Milk, 100m));
        await using (var db = Factory.CreateDbContext())
        {
            db.FridgeItems.Add(new Core.Fridges.FridgeItem
            {
                Id = Id(item),
                FridgeId = fridgeId,
                ProductId = Milk,
                PackageAmount = 1000m,
                RemainingPercent = 10m,
                AddedById = anna.UserId,
                CreatedAt = TestData.Now,
                UpdatedAt = TestData.Now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions/{Id(deduction)}/undo", null), HttpStatusCode.Conflict, "deduction.items_changed");
    }

    [Fact]
    public async Task Undo_SameDeductionConcurrently_OneSucceedsTheOthersGet409()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddItemAsync(anna, fridgeId, Milk, 1000m, 10m);
        await AddItemAsync(anna, fridgeId, Cream, 1000m, 50m);
        var deduction = await DeductAsync(anna, fridgeId, null, null, (Milk, 100m), (Cream, 100m));

        // Undo re-inserts the emptied milk package under its old id, so simultaneous undos race to the primary key.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions/{Id(deduction)}/undo", null)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        foreach (var lost in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            var problem = await ApiClient.ReadJsonAsync(lost);
            lost.StatusCode.ShouldBe(HttpStatusCode.Conflict, problem.ToString());
            problem.GetProperty("code").GetString().ShouldBeOneOf("deduction.already_undone", "deduction.items_changed");
        }

        var items = await ItemsAsync(anna, fridgeId);
        items.GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Deduct_WithRecipeAndVariant_IncrementsCookCountsAndUndoKeepsThem()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddItemAsync(anna, fridgeId, Milk, 1000m);
        var (recipeId, variantId) = await AddRecipeWithVariantAsync(anna.UserId, archived: false);

        var deduction = await DeductAsync(anna, fridgeId, recipeId, variantId, (Milk, 100m));
        (await CookCountsAsync(recipeId, variantId)).ShouldBe((1, 1));

        (await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions/{Id(deduction)}/undo", null)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await CookCountsAsync(recipeId, variantId)).ShouldBe((1, 1));

        await DeductAsync(anna, fridgeId, recipeId, null, (Milk, 100m));
        (await CookCountsAsync(recipeId, variantId)).ShouldBe((2, 1));
    }

    [Fact]
    public async Task Deduct_ArchivedRecipeTheCookCanStillRead_Works()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddItemAsync(anna, fridgeId, Milk, 1000m);
        var (recipeId, variantId) = await AddRecipeWithVariantAsync(anna.UserId, archived: true);

        await DeductAsync(anna, fridgeId, recipeId, variantId, (Milk, 100m));

        (await CookCountsAsync(recipeId, variantId)).ShouldBe((1, 1));
    }

    [Fact]
    public async Task Deduct_RecipeTheCallerCannotSee_Returns404()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddItemAsync(anna, fridgeId, Milk, 1000m);
        var (recipeId, _) = await AddRecipeWithVariantAsync(bartek.UserId, archived: false);

        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions", new { recipeId, variantId = (Guid?)null, lines = new[] { new { productId = Milk, amount = 100 } } }),
            HttpStatusCode.NotFound,
            "recipe.not_found");
        (await ItemsAsync(anna, fridgeId))[0].GetProperty("remainingPercent").GetDecimal().ShouldBe(100m);
    }

    [Fact]
    public async Task Deduct_NoLinesOrNothingInTheFridge_Returns400Empty()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);

        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions", new { recipeId = (Guid?)null, variantId = (Guid?)null, lines = Array.Empty<object>() }),
            HttpStatusCode.BadRequest,
            "deduction.empty");
        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions", new { recipeId = (Guid?)null, variantId = (Guid?)null, lines = new[] { new { productId = Milk, amount = 100 } } }),
            HttpStatusCode.BadRequest,
            "deduction.empty");
    }

    [Fact]
    public async Task Deduct_RepeatedProductOrVariantWithoutRecipe_Returns400()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddItemAsync(anna, fridgeId, Milk, 1000m);

        var problem = await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions", new
            {
                recipeId = (Guid?)null,
                variantId = Guid.CreateVersion7(),
                lines = new[] { new { productId = Milk, amount = 100m }, new { productId = Milk, amount = 0m } },
            }),
            HttpStatusCode.BadRequest,
            "validation.failed");

        var errors = problem.GetProperty("errors");
        errors.GetProperty("recipeId")[0].GetString().ShouldBe("required");
        errors.GetProperty("lines[1].productId")[0].GetString().ShouldBe("duplicate");
        errors.GetProperty("lines[1].amount")[0].GetString().ShouldBe("out_of_range");
    }

    [Fact]
    public async Task Undo_UnknownDeduction_Returns404()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);

        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions/{Guid.CreateVersion7()}/undo", null), HttpStatusCode.NotFound, "deduction.not_found");
    }

    private static async Task<JsonElement> DeductAsync(ApiClient client, Guid fridgeId, Guid? recipeId, Guid? variantId, params (Guid ProductId, decimal Amount)[] lines)
    {
        var response = await client.PostJsonAsync($"/api/v1/fridges/{fridgeId}/deductions", new
        {
            recipeId,
            variantId,
            lines = lines.Select(l => new { productId = l.ProductId, amount = l.Amount }).ToArray(),
        });
        var result = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "DeductionResult");
        response.Headers.Location.ShouldNotBeNull();
        return result;
    }

    private async Task<(Guid RecipeId, Guid VariantId)> AddRecipeWithVariantAsync(Guid authorId, bool archived)
    {
        await using var db = Factory.CreateDbContext();
        var recipe = new Recipe
        {
            AuthorId = authorId,
            Title = "Naleśniki",
            NormalizedTitle = NameNormalizer.Normalize("Naleśniki"),
            Visibility = VisibilityLevel.Private,
            IsArchived = archived,
            CreatedAt = TestData.Now,
            UpdatedAt = TestData.Now,
        };
        var variant = new Variant
        {
            RecipeId = recipe.Id,
            AuthorId = authorId,
            Name = "Na mleku owsianym",
            Visibility = VisibilityLevel.Private,
            CreatedAt = TestData.Now,
            UpdatedAt = TestData.Now,
        };
        db.Recipes.Add(recipe);
        db.Variants.Add(variant);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (recipe.Id, variant.Id);
    }

    private async Task<(int Recipe, int Variant)> CookCountsAsync(Guid recipeId, Guid variantId)
    {
        await using var db = Factory.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;
        var recipe = await db.RecipeCookCounts.Where(c => c.RecipeId == recipeId).Select(c => c.Count).SingleOrDefaultAsync(ct);
        var variant = await db.VariantCookCounts.Where(c => c.VariantId == variantId).Select(c => c.Count).SingleOrDefaultAsync(ct);
        return (recipe, variant);
    }

    private static Guid Id(JsonElement e) => e.GetProperty("id").GetGuid();

    /// <summary>Everything but the version (xmin changes on every write, including the re-insert).</summary>
    private static List<string> Comparable(JsonElement items) =>
        [.. items.EnumerateArray().Select(i =>
        {
            var copy = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(i.GetRawText())!;
            copy.Remove("version");
            return JsonSerializer.Serialize(copy);
        })];
}
