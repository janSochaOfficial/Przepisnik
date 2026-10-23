using System.Net;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Recipes;

/// <summary>Visibility reads and PUT /recipes/{id}/visibility (PROJECT §4.4).</summary>
public sealed class RecipeVisibilityTests(ApiFactory factory) : RecipeTestBase(factory)
{
    [Fact]
    public async Task Get_FridgesRecipe_IsVisibleToAFridgeMateAnd404ForAStrangerAndAfterLeavingTheFridge()
    {
        using var ola = await LoginAsync("ola");
        using var mate = await LoginAsync("mate");
        using var stranger = await LoginAsync("stranger");
        var fridge = await AddFridgeAsync(ola.UserId, mate.UserId);
        var body = Draft("Dla lodówki");
        body["visibility"] = "fridges";
        var id = (await CreateAsync(ola, body)).GetProperty("id").GetGuid();

        var seen = await ContractAssert.IsJsonAsync(await GetRecipeAsync(mate, id), HttpStatusCode.OK, "RecipeDetail");
        seen.GetProperty("canEdit").GetBoolean().ShouldBeFalse();
        var list = await ContractAssert.IsJsonAsync(await mate.GetAsync("/api/v1/recipes"), HttpStatusCode.OK, "PagedRecipeSummary");
        list.GetProperty("totalCount").GetInt32().ShouldBe(1);
        await ContractAssert.IsProblemAsync(await GetRecipeAsync(stranger, id), HttpStatusCode.NotFound, "recipe.not_found");

        await RemoveFromFridgeAsync(fridge, mate.UserId);

        await ContractAssert.IsProblemAsync(await GetRecipeAsync(mate, id), HttpStatusCode.NotFound, "recipe.not_found");
        var emptied = await ContractAssert.IsJsonAsync(await mate.GetAsync("/api/v1/recipes"), HttpStatusCode.OK, "PagedRecipeSummary");
        emptied.GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Get_PrivateRecipe_Is404ForEveryoneButTheAuthor()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var id = (await CreateAsync(ola, Draft())).GetProperty("id").GetGuid();

        (await GetRecipeAsync(ola, id)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.IsProblemAsync(await GetRecipeAsync(kasia, id), HttpStatusCode.NotFound, "recipe.not_found");
        await ContractAssert.IsProblemAsync(await kasia.PutJsonAsync($"/api/v1/recipes/{id}/visibility", new { visibility = "public" }), HttpStatusCode.NotFound, "recipe.not_found");
    }

    [Fact]
    public async Task SetVisibility_NarrowingPublicToPrivateWithAForeignVariant_Returns409()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var body = Draft();
        body["visibility"] = "public";
        var id = (await CreateAsync(ola, body)).GetProperty("id").GetGuid();
        await AddVariantAsync(id, kasia.UserId, "Kasi");

        var response = await ola.PutJsonAsync($"/api/v1/recipes/{id}/visibility", new { visibility = "private" });

        var problem = await ContractAssert.IsProblemAsync(response, HttpStatusCode.Conflict, "recipe.visibility_locked");
        problem.GetProperty("foreignVariantCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task SetVisibility_NarrowingWithoutForeignVariants_Returns200()
    {
        using var ola = await LoginAsync("ola");
        var body = Draft();
        body["visibility"] = "public";
        var id = (await CreateAsync(ola, body)).GetProperty("id").GetGuid();
        await AddVariantAsync(id, ola.UserId, "Moja");

        var updated = await ContractAssert.IsJsonAsync(
            await ola.PutJsonAsync($"/api/v1/recipes/{id}/visibility", new { visibility = "private" }), HttpStatusCode.OK, "RecipeDetail");

        updated.GetProperty("visibility").GetString().ShouldBe("private");
        updated.GetProperty("canEdit").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task SetVisibility_WideningWithForeignVariants_IsAlwaysAllowed()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var body = Draft();
        body["visibility"] = "fridges";
        var id = (await CreateAsync(ola, body)).GetProperty("id").GetGuid();
        await AddFridgeAsync(ola.UserId, kasia.UserId);
        await AddVariantAsync(id, kasia.UserId, "Kasi");

        var updated = await ContractAssert.IsJsonAsync(
            await ola.PutJsonAsync($"/api/v1/recipes/{id}/visibility", new { visibility = "public" }), HttpStatusCode.OK, "RecipeDetail");

        updated.GetProperty("visibility").GetString().ShouldBe("public");
        updated.GetProperty("foreignVariantCount").GetInt32().ShouldBe(1);
        updated.GetProperty("totalVariantCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task SetVisibility_ByAnotherUser_Returns403()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var body = Draft();
        body["visibility"] = "public";
        var id = (await CreateAsync(ola, body)).GetProperty("id").GetGuid();

        var response = await kasia.PutJsonAsync($"/api/v1/recipes/{id}/visibility", new { visibility = "private" });

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.Forbidden, "recipe.not_author");
    }

    [Fact]
    public async Task Detail_VariantCounts_SplitVisibleFromTotalForTheAuthor()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var body = Draft();
        body["visibility"] = "public";
        var id = (await CreateAsync(ola, body)).GetProperty("id").GetGuid();
        await AddVariantAsync(id, kasia.UserId, "Prywatny", VisibilityLevel.Private);
        await AddVariantAsync(id, kasia.UserId, "Publiczny", VisibilityLevel.Public);

        var author = await ContractAssert.IsJsonAsync(await GetRecipeAsync(ola, id), HttpStatusCode.OK, "RecipeDetail");
        var other = await ContractAssert.IsJsonAsync(await GetRecipeAsync(kasia, id), HttpStatusCode.OK, "RecipeDetail");
        using var third = await LoginAsync("third");
        var third_ = await ContractAssert.IsJsonAsync(await GetRecipeAsync(third, id), HttpStatusCode.OK, "RecipeDetail");

        author.GetProperty("totalVariantCount").GetInt32().ShouldBe(2);
        author.GetProperty("foreignVariantCount").GetInt32().ShouldBe(2);
        author.GetProperty("variantCount").GetInt32().ShouldBe(1);
        other.GetProperty("variantCount").GetInt32().ShouldBe(2);
        other.GetProperty("totalVariantCount").GetInt32().ShouldBe(0);
        third_.GetProperty("variantCount").GetInt32().ShouldBe(1);
    }
}
