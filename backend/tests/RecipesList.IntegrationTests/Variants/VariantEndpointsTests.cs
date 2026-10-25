using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Favorites;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Variants;

/// <summary>API.md §5: variant CRUD, the resolved view, the matrix and visibility. Recipes are arranged in the database.</summary>
public sealed class VariantEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly VariantScenario _s = new();
    private ApiClient _ania = null!;
    private ApiClient _bartek = null!;
    private ApiClient _stranger = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _ania = factory.CreateApiClient();
        _bartek = factory.CreateApiClient();
        _stranger = factory.CreateApiClient();
        await _ania.LoginAsync("ania");
        await _bartek.LoginAsync("bartek");
        await _stranger.LoginAsync("obcy");
        await using var db = factory.CreateDbContext();
        await _s.SeedCatalogAsync(db);
    }

    public ValueTask DisposeAsync()
    {
        _ania.Dispose();
        _bartek.Dispose();
        _stranger.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Create_EditedCopy_Returns201AndTheMatrixContainsIt()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var copy = VariantScenario.CopyOf(recipe);
        var cream = recipe.Ingredients[1];
        copy.Ingredients[1]["productId"] = _s.HeavyCream.Id;
        copy.Ingredients.Insert(1, EditedCopy.NewIngredient(_s.Pepper.Id, EditedCopy.Quantity(2, "piece", "large")));

        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write("Na śmietance", copy));

        var variant = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantDetail");
        var id = variant.GetProperty("id").GetGuid();
        response.Headers.Location!.ToString().ShouldBe($"/api/v1/variants/{id}");
        variant.GetProperty("author").GetProperty("nickname").GetString().ShouldBe("bartek");
        variant.GetProperty("changeCount").GetInt32().ShouldBe(2);
        variant.GetProperty("baseAvailable").GetBoolean().ShouldBeTrue();
        var ops = variant.GetProperty("ingredientOps").EnumerateArray().ToList();
        ops.Select(o => o.GetProperty("op").GetString()).ShouldBe(["modify", "add"]);
        ops[0].GetProperty("targetId").GetGuid().ShouldBe(cream.Id);
        ops[1].GetProperty("afterId").GetGuid().ShouldBe(recipe.Ingredients[0].Id);

        var matrix = await ContractAssert.IsJsonAsync(await _bartek.GetAsync($"/api/v1/recipes/{recipe.Id}/variant-matrix"), HttpStatusCode.OK, "VariantMatrix");
        matrix.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("id").GetString()).ShouldBe(["base", id.ToString()]);
        var swap = matrix.GetProperty("variants")[1].GetProperty("productSwaps").EnumerateArray().ShouldHaveSingleItem();
        swap.GetProperty("toProductId").GetGuid().ShouldBe(_s.HeavyCream.Id);
        var slots = matrix.GetProperty("slots").EnumerateArray().ToList();
        slots.Select(s => s.GetProperty("key").GetString()).ShouldBe(
            [$"i:{recipe.Ingredients[0].Id}", $"i:{cream.Id}", $"i:{recipe.Ingredients[2].Id}", $"p:{_s.Pepper.Id}"]);
        slots[3].GetProperty("afterSlotKey").GetString().ShouldBe($"i:{recipe.Ingredients[0].Id}");
        slots[3].GetProperty("label").GetString().ShouldBe("papryka czerwona");
        slots[3].GetProperty("options")[1].GetProperty("key").GetString().ShouldBe($"{_s.Pepper.Id}:2.6");
        slots[1].GetProperty("options").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Create_UnchangedCopy_Returns400NoChanges()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);

        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write("Bez zmian", VariantScenario.CopyOf(recipe)));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "variant.no_changes");
    }

    [Fact]
    public async Task Create_SameNameTwiceBySameAuthor_Returns409NameTaken()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        await CreateAsync(_bartek, recipe, "Ostrzejsza");

        var copy = VariantScenario.CopyOf(recipe);
        copy.CookMinutes = 5;
        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write(" Ostrzejsza ", copy));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Conflict, "variant.name_taken");
        (await CreateAsync(_ania, recipe, "Ostrzejsza")).GetProperty("name").GetString().ShouldBe("Ostrzejsza");
    }

    [Fact]
    public async Task Create_SameNameConcurrently_OneWinsTheOthersGet409NameTaken()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var copy = VariantScenario.CopyOf(recipe);
        copy.CookMinutes = 5;
        var body = VariantScenario.Write("Wyścig", copy);

        // The name check runs before the insert, so simultaneous requests race to the unique index.
        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", body)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        foreach (var lost in responses.Where(r => r.StatusCode != HttpStatusCode.Created))
        {
            await ContractAssert.IsProblemAsync(lost, HttpStatusCode.Conflict, "variant.name_taken");
        }
    }

    [Fact]
    public async Task Create_UnitTheProductCannotUse_Returns400UnitIncompatible()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var copy = VariantScenario.CopyOf(recipe);
        copy.Ingredients[2]["quantity"] = EditedCopy.Quantity(1, "piece");

        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write("Sól na sztuki", copy));

        var problem = await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "quantity.unit_incompatible");
        problem.GetProperty("errors").GetProperty("recipe.ingredients[2].quantity")[0].GetString().ShouldBe("quantity.unit_incompatible");
    }

    [Fact]
    public async Task Create_ReorderedBaseRows_Returns400Validation()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var copy = VariantScenario.CopyOf(recipe);
        (copy.Ingredients[0], copy.Ingredients[1]) = (copy.Ingredients[1], copy.Ingredients[0]);

        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write("Odwrotnie", copy));

        var problem = await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "validation.failed");
        problem.GetProperty("errors").TryGetProperty("recipe.ingredients[1].id", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Create_RecipeNotVisible_Returns404()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Private);
        var copy = VariantScenario.CopyOf(recipe);
        copy.CookMinutes = 5;

        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write("Cudzy", copy));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.NotFound, "recipe.not_found");
    }

    [Fact]
    public async Task Create_PhotoOfAnotherUser_Returns400ImageNotOwned()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var image = new Core.Images.Image { OwnerId = _ania.UserId, Width = 10, Height = 10, CreatedAt = TestData.Now };
        await using (var db = factory.CreateDbContext())
        {
            db.Images.Add(image);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var copy = VariantScenario.CopyOf(recipe);
        copy.InstructionSteps[0]["blocks"] = new object[] { new { type = "image", imageId = image.Id, caption = (string?)null } };

        var response = await _bartek.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write("Z cudzym zdjęciem", copy));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "image.not_owned");
    }

    [Fact]
    public async Task Update_ByAuthor_ReDiffsAndBumpsTheVersion()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var created = await CreateAsync(_bartek, recipe, "Krócej");
        var copy = VariantScenario.CopyOf(recipe);
        copy.Ingredients.RemoveAt(2);
        copy.InstructionSteps.Insert(0, EditedCopy.NewStep("Rozgrzej patelnię."));

        var response = await _bartek.PutJsonAsync(
            $"/api/v1/variants/{created.GetProperty("id").GetGuid()}",
            VariantScenario.Write("Bez soli", copy, "private", created.GetProperty("version").GetUInt32()));

        var updated = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "VariantDetail");
        updated.GetProperty("name").GetString().ShouldBe("Bez soli");
        updated.GetProperty("visibility").GetString().ShouldBe("private");
        updated.GetProperty("cookMinutes").ValueKind.ShouldBe(JsonValueKind.Null);
        updated.GetProperty("ingredientOps").EnumerateArray().ShouldHaveSingleItem().GetProperty("op").GetString().ShouldBe("remove");
        updated.GetProperty("stepOps")[0].GetProperty("section").GetString().ShouldBe("instructions");
        updated.GetProperty("version").GetUInt32().ShouldNotBe(created.GetProperty("version").GetUInt32());
    }

    [Fact]
    public async Task Update_ByNonAuthor_Returns403()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var created = await CreateAsync(_bartek, recipe, "Krócej");
        var copy = VariantScenario.CopyOf(recipe);
        copy.CookMinutes = 1;

        var response = await _ania.PutJsonAsync(
            $"/api/v1/variants/{created.GetProperty("id").GetGuid()}",
            VariantScenario.Write("Przejęty", copy, "public", created.GetProperty("version").GetUInt32()));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Forbidden, "variant.not_author");
    }

    [Fact]
    public async Task Update_StaleVersion_Returns409ConcurrencyConflict()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var created = await CreateAsync(_bartek, recipe, "Krócej");
        var copy = VariantScenario.CopyOf(recipe);
        copy.CookMinutes = 1;

        var response = await _bartek.PutJsonAsync(
            $"/api/v1/variants/{created.GetProperty("id").GetGuid()}",
            VariantScenario.Write("Krócej", copy, "public", created.GetProperty("version").GetUInt32() + 1));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Conflict, "concurrency.conflict");
    }

    [Fact]
    public async Task Visibility_PrivateVariantOnPublicRecipe_OnlyItsAuthorSeesIt()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var id = (await CreateAsync(_bartek, recipe, "Mój sekret", "private")).GetProperty("id").GetGuid();

        var aniaMatrix = await ContractAssert.IsJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}/variant-matrix"), HttpStatusCode.OK, "VariantMatrix");
        aniaMatrix.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("id").GetString()).ShouldBe(["base"]);
        var bartekMatrix = await ContractAssert.IsJsonAsync(await _bartek.GetAsync($"/api/v1/recipes/{recipe.Id}/variant-matrix"), HttpStatusCode.OK, "VariantMatrix");
        bartekMatrix.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("id").GetString()).ShouldBe(["base", id.ToString()]);

        (await ApiClient.ReadJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}/variants"))).GetArrayLength().ShouldBe(0);
        await ContractAssert.IsProblemAsync(await _ania.GetAsync($"/api/v1/variants/{id}"), HttpStatusCode.NotFound, "variant.not_found");
        await ContractAssert.IsProblemAsync(await _ania.DeleteAsync($"/api/v1/variants/{id}"), HttpStatusCode.NotFound, "variant.not_found");
        await ContractAssert.IsJsonAsync(await _bartek.GetAsync($"/api/v1/variants/{id}"), HttpStatusCode.OK, "VariantDetail");
    }

    [Fact]
    public async Task Visibility_PublicVariantOnFridgeRecipe_IsInvisibleToAStranger()
    {
        await using (var db = factory.CreateDbContext())
        {
            await VariantScenario.ShareFridgeAsync(db, _ania.UserId, _bartek.UserId);
        }

        var recipe = await AddRecipeAsync(VisibilityLevel.Fridges);
        var id = (await CreateAsync(_bartek, recipe, "Publiczny", "public")).GetProperty("id").GetGuid();

        await ContractAssert.IsProblemAsync(await _stranger.GetAsync($"/api/v1/recipes/{recipe.Id}/variant-matrix"), HttpStatusCode.NotFound, "recipe.not_found");
        await ContractAssert.IsProblemAsync(await _stranger.GetAsync($"/api/v1/recipes/{recipe.Id}/variants"), HttpStatusCode.NotFound, "recipe.not_found");
        await ContractAssert.IsProblemAsync(await _stranger.GetAsync($"/api/v1/variants/{id}"), HttpStatusCode.NotFound, "variant.not_found");
        await ContractAssert.IsProblemAsync(await _stranger.GetAsync($"/api/v1/variants/{id}/resolved"), HttpStatusCode.NotFound, "variant.not_found");
        var list = await ApiClient.ReadJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}/variants"));
        ContractAssert.MatchesSchema(list.EnumerateArray().ShouldHaveSingleItem(), "VariantSummary");
    }

    [Fact]
    public async Task Resolved_Variant_AppliesOpsAndComputesTheQuickTag()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var copy = VariantScenario.CopyOf(recipe);
        copy.CookMinutes = 15;
        copy.UtensilIds.Add(_s.Oven.Id);
        copy.Ingredients[0]["quantity"] = EditedCopy.Quantity(0.5m, "kg");
        var id = (await CreateAsync(_bartek, recipe, "Szybka", copy: copy)).GetProperty("id").GetGuid();

        var resolved = await ContractAssert.IsJsonAsync(await _bartek.GetAsync($"/api/v1/variants/{id}/resolved"), HttpStatusCode.OK, "ResolvedRecipe");

        resolved.GetProperty("id").GetGuid().ShouldBe(recipe.Id);
        resolved.GetProperty("variantId").GetGuid().ShouldBe(id);
        resolved.GetProperty("variantName").GetString().ShouldBe("Szybka");
        resolved.GetProperty("cookMinutes").GetInt32().ShouldBe(15);
        resolved.GetProperty("tags").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ShouldBe(["szybkie", "obiad"]);
        resolved.GetProperty("utensils").EnumerateArray().Select(u => u.GetProperty("name").GetString()).ShouldBe(["patelnia", "piekarnik"]);
        var chicken = resolved.GetProperty("ingredients")[0];
        chicken.GetProperty("id").GetGuid().ShouldBe(recipe.Ingredients[0].Id);
        chicken.GetProperty("baseAmount").GetDecimal().ShouldBe(500m);
        chicken.GetProperty("product").GetProperty("name").GetString().ShouldBe("pierś z kurczaka");
        resolved.GetProperty("instructionSteps").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task ListMine_BaseNoLongerVisible_IsListedWithBaseAvailableFalse()
    {
        Guid fridgeId;
        await using (var db = factory.CreateDbContext())
        {
            fridgeId = await VariantScenario.ShareFridgeAsync(db, _ania.UserId, _bartek.UserId);
        }

        var recipe = await AddRecipeAsync(VisibilityLevel.Fridges);
        var id = (await CreateAsync(_bartek, recipe, "Z lodówki")).GetProperty("id").GetGuid();
        await using (var db = factory.CreateDbContext())
        {
            await db.FridgeMembers.Where(m => m.FridgeId == fridgeId && m.UserId == _bartek.UserId).ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        var page = await ContractAssert.IsJsonAsync(await _bartek.GetAsync("/api/v1/variants?author=me"), HttpStatusCode.OK, "PagedVariantSummary");

        page.GetProperty("totalCount").GetInt32().ShouldBe(1);
        page.GetProperty("pageSize").GetInt32().ShouldBe(20);
        var item = page.GetProperty("items")[0];
        item.GetProperty("id").GetGuid().ShouldBe(id);
        item.GetProperty("baseAvailable").GetBoolean().ShouldBeFalse();
        (await _bartek.GetAsync($"/api/v1/variants/{id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.IsProblemAsync(await _bartek.GetAsync($"/api/v1/variants/{id}/resolved"), HttpStatusCode.NotFound, "variant.not_found");

        var copy = VariantScenario.CopyOf(recipe);
        copy.CookMinutes = 1;
        var detail = await ApiClient.ReadJsonAsync(await _bartek.GetAsync($"/api/v1/variants/{id}"));
        await ContractAssert.IsProblemAsync(
            await _bartek.PutJsonAsync($"/api/v1/variants/{id}", VariantScenario.Write("Z lodówki", copy, "public", detail.GetProperty("version").GetUInt32())),
            HttpStatusCode.NotFound,
            "recipe.not_found");
        (await _bartek.DeleteAsync($"/api/v1/variants/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ListMine_WithoutAuthorMe_Returns400()
    {
        await ContractAssert.IsProblemAsync(await _bartek.GetAsync("/api/v1/variants"), HttpStatusCode.BadRequest, "validation.failed");
        await ContractAssert.IsProblemAsync(await _bartek.GetAsync("/api/v1/variants?author=me&pageSize=101"), HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task ListMine_Paging_NewestFirst()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        foreach (var name in new[] { "A", "B", "C" })
        {
            await CreateAsync(_bartek, recipe, name);
        }

        var page = await ContractAssert.IsJsonAsync(await _bartek.GetAsync("/api/v1/variants?author=me&page=1&pageSize=2"), HttpStatusCode.OK, "PagedVariantSummary");

        page.GetProperty("totalCount").GetInt32().ShouldBe(3);
        page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("name").GetString()).ShouldBe(["C", "B"]);
    }

    [Fact]
    public async Task Favorites_AreReflectedInSummariesAndTheMatrix()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var id = (await CreateAsync(_bartek, recipe, "Ulubiony")).GetProperty("id").GetGuid();
        await using (var db = factory.CreateDbContext())
        {
            db.VariantFavorites.Add(new VariantFavorite { UserId = _ania.UserId, VariantId = id, CreatedAt = TestData.Now });
            db.RecipeFavorites.Add(new RecipeFavorite { UserId = _ania.UserId, RecipeId = recipe.Id, CreatedAt = TestData.Now });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var matrix = await ApiClient.ReadJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}/variant-matrix"));
        matrix.GetProperty("variants").EnumerateArray().Select(v => v.GetProperty("isFavorite").GetBoolean()).ShouldBe([true, true]);
        (await ApiClient.ReadJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipe.Id}/variants")))[0].GetProperty("isFavorite").GetBoolean().ShouldBeTrue();
        (await ApiClient.ReadJsonAsync(await _bartek.GetAsync($"/api/v1/recipes/{recipe.Id}/variants")))[0].GetProperty("isFavorite").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Delete_ByNonAuthorWhoSeesIt_Returns403_ByAuthor_Returns204()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var id = (await CreateAsync(_bartek, recipe, "Do usunięcia")).GetProperty("id").GetGuid();

        await ContractAssert.IsProblemAsync(await _ania.DeleteAsync($"/api/v1/variants/{id}"), HttpStatusCode.Forbidden, "variant.not_author");
        (await _bartek.DeleteAsync($"/api/v1/variants/{id}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.IsProblemAsync(await _bartek.GetAsync($"/api/v1/variants/{id}"), HttpStatusCode.NotFound, "variant.not_found");
    }

    [Fact]
    public async Task SetVisibility_ByAuthor_Returns200WithNewVisibilityAndVersion()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var created = await CreateAsync(_bartek, recipe, "Widoczność", "public");
        var id = created.GetProperty("id").GetGuid();

        var response = await _bartek.PutJsonAsync($"/api/v1/variants/{id}/visibility", new { visibility = "private" });

        var updated = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "VariantDetail");
        updated.GetProperty("visibility").GetString().ShouldBe("private");
        updated.GetProperty("version").GetUInt32().ShouldNotBe(created.GetProperty("version").GetUInt32());
        await ContractAssert.IsProblemAsync(await _ania.GetAsync($"/api/v1/variants/{id}"), HttpStatusCode.NotFound, "variant.not_found");
    }

    [Fact]
    public async Task SetVisibility_ByAnotherUser_Returns403_WhenInvisible404_WhenInvalid400()
    {
        var recipe = await AddRecipeAsync(VisibilityLevel.Public);
        var id = (await CreateAsync(_bartek, recipe, "Cudza", "public")).GetProperty("id").GetGuid();
        var privateId = (await CreateAsync(_bartek, recipe, "Prywatna", "private")).GetProperty("id").GetGuid();

        await ContractAssert.IsProblemAsync(await _ania.PutJsonAsync($"/api/v1/variants/{id}/visibility", new { visibility = "private" }), HttpStatusCode.Forbidden, "variant.not_author");
        await ContractAssert.IsProblemAsync(await _ania.PutJsonAsync($"/api/v1/variants/{privateId}/visibility", new { visibility = "public" }), HttpStatusCode.NotFound, "variant.not_found");
        await ContractAssert.IsProblemAsync(await _ania.PutJsonAsync($"/api/v1/variants/{Guid.NewGuid()}/visibility", new { visibility = "public" }), HttpStatusCode.NotFound, "variant.not_found");
        (await _bartek.PutJsonAsync($"/api/v1/variants/{id}/visibility", new { visibility = "nope" })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _bartek.PutJsonAsync($"/api/v1/variants/{id}/visibility", new { })).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task<Recipe> AddRecipeAsync(VisibilityLevel visibility, string title = "Kurczak w śmietanie")
    {
        await using var db = factory.CreateDbContext();
        return await _s.AddRecipeAsync(db, _ania.UserId, visibility, title);
    }

    /// <summary>Creates a variant whose only change is a shorter cook time (or the given copy).</summary>
    private static async Task<JsonElement> CreateAsync(ApiClient client, Recipe recipe, string name, string visibility = "public", EditedCopy? copy = null)
    {
        if (copy is null)
        {
            copy = VariantScenario.CopyOf(recipe);
            copy.CookMinutes = 20;
        }

        var response = await client.PostJsonAsync($"/api/v1/recipes/{recipe.Id}/variants", VariantScenario.Write(name, copy, visibility));
        return await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantDetail");
    }
}
