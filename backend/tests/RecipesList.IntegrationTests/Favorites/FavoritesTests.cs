using System.Data.Common;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using RecipesList.Core.Common;
using RecipesList.Core.Favorites;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Infrastructure;
using RecipesList.IntegrationTests.Recipes;

namespace RecipesList.IntegrationTests.Favorites;

/// <summary>API.md §4, §5, §5.3 / PROJECT §4.6: ♥ on recipes and variants and the merged "Ulubione" list.</summary>
public sealed class FavoritesTests(ApiFactory factory) : RecipeTestBase(factory)
{
    [Fact]
    public async Task Favorite_RecipeAndVariant_AreListedNewestFirstWithIsFavoriteEverywhere()
    {
        using var ania = await LoginAsync("ania");
        using var bartek = await LoginAsync("bartek");
        var recipeA = await CreateRecipeAsync(ania, "Zupa", "public");
        var recipeB = await CreateRecipeAsync(ania, "Pierogi", "public");
        var variant = await AddVariantAsync(recipeA, bartek.UserId, "Ostra", VisibilityLevel.Public);

        (await bartek.PutJsonAsync($"/api/v1/recipes/{recipeB}/favorite", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        (await bartek.PutJsonAsync($"/api/v1/variants/{variant}/favorite", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await Task.Delay(5, TestContext.Current.CancellationToken);
        (await bartek.PutJsonAsync($"/api/v1/recipes/{recipeA}/favorite", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var list = await ListAsync(bartek, "");
        list.GetProperty("totalCount").GetInt32().ShouldBe(3);
        var items = list.GetProperty("items").EnumerateArray().ToList();
        items.Select(i => i.GetProperty("kind").GetString()).ShouldBe(["recipe", "variant", "recipe"]);
        items[0].GetProperty("recipe").GetProperty("id").GetGuid().ShouldBe(recipeA);
        items[1].GetProperty("variant").GetProperty("id").GetGuid().ShouldBe(variant);
        items[1].GetProperty("variant").GetProperty("isFavorite").GetBoolean().ShouldBeTrue();
        items[1].GetProperty("recipe").GetProperty("id").GetGuid().ShouldBe(recipeA);
        items[2].GetProperty("recipe").GetProperty("id").GetGuid().ShouldBe(recipeB);

        var recipes = await ContractAssert.IsJsonAsync(await bartek.GetAsync("/api/v1/recipes"), HttpStatusCode.OK, "PagedRecipeSummary");
        recipes.GetProperty("items").EnumerateArray().ShouldAllBe(r => r.GetProperty("isFavorite").GetBoolean());
        var detail = await ContractAssert.IsJsonAsync(await bartek.GetAsync($"/api/v1/recipes/{recipeA}"), HttpStatusCode.OK, "RecipeDetail");
        detail.GetProperty("isFavorite").GetBoolean().ShouldBeTrue();
        var variants = await ApiClient.ReadJsonAsync(await bartek.GetAsync($"/api/v1/recipes/{recipeA}/variants"));
        variants.EnumerateArray().Single().GetProperty("isFavorite").GetBoolean().ShouldBeTrue();
        var matrix = await ContractAssert.IsJsonAsync(await bartek.GetAsync($"/api/v1/recipes/{recipeA}/variant-matrix"), HttpStatusCode.OK, "VariantMatrix");
        matrix.GetProperty("variants").EnumerateArray()
            .Single(v => v.GetProperty("id").GetString() == variant.ToString())
            .GetProperty("isFavorite").GetBoolean().ShouldBeTrue();

        using var other = await LoginAsync("celina");
        (await ListAsync(other, "")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task PutAndDelete_AreIdempotent()
    {
        using var ania = await LoginAsync("ania");
        var recipe = await CreateRecipeAsync(ania, "Zupa", "private");

        for (var i = 0; i < 2; i++)
        {
            (await ania.PutJsonAsync($"/api/v1/recipes/{recipe}/favorite", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await ListAsync(ania, "")).GetProperty("totalCount").GetInt32().ShouldBe(1);

        for (var i = 0; i < 2; i++)
        {
            (await ania.DeleteAsync($"/api/v1/recipes/{recipe}/favorite")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        (await ania.DeleteAsync($"/api/v1/variants/{Guid.NewGuid()}/favorite")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(ania, "")).GetProperty("totalCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Put_InvisibleRecipeOrVariant_Returns404()
    {
        using var ania = await LoginAsync("ania");
        using var bartek = await LoginAsync("bartek");
        var recipe = await CreateRecipeAsync(ania, "Tajne", "private");
        var variant = await AddVariantAsync(recipe, ania.UserId, "Prywatny");

        await ContractAssert.IsProblemAsync(await bartek.PutJsonAsync($"/api/v1/recipes/{recipe}/favorite", null), HttpStatusCode.NotFound, "recipe.not_found");
        await ContractAssert.IsProblemAsync(await bartek.PutJsonAsync($"/api/v1/variants/{variant}/favorite", null), HttpStatusCode.NotFound, "variant.not_found");
        await ContractAssert.IsProblemAsync(await bartek.PutJsonAsync($"/api/v1/recipes/{Guid.NewGuid()}/favorite", null), HttpStatusCode.NotFound, "recipe.not_found");
    }

    [Fact]
    public async Task List_RecipeNarrowedToPrivate_DisappearsAndReappearsWhenWidened()
    {
        using var ania = await LoginAsync("ania");
        using var bartek = await LoginAsync("bartek");
        var id = await CreateRecipeAsync(ania, "Zupa", "public");
        (await bartek.PutJsonAsync($"/api/v1/recipes/{id}/favorite", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(bartek, "")).GetProperty("totalCount").GetInt32().ShouldBe(1);

        // Public recipes are locked for edits; narrowing is done through the visibility endpoint.
        await SetVisibilityAsync(ania, id, "private");
        (await ListAsync(bartek, "")).GetProperty("totalCount").GetInt32().ShouldBe(0);
        await using (var db = Factory.CreateDbContext())
        {
            (await db.RecipeFavorites.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
        }

        await SetVisibilityAsync(ania, id, "public");
        var list = await ListAsync(bartek, "");
        list.GetProperty("totalCount").GetInt32().ShouldBe(1);
        list.GetProperty("items")[0].GetProperty("recipe").GetProperty("id").GetGuid().ShouldBe(id);
    }

    [Fact]
    public async Task List_VariantWhoseBaseBecameInvisible_IsHiddenNotDeleted()
    {
        using var ania = await LoginAsync("ania");
        using var bartek = await LoginAsync("bartek");
        var recipe = await CreateRecipeAsync(ania, "Zupa", "public");
        var variant = await AddVariantAsync(recipe, ania.UserId, "Ostra", VisibilityLevel.Public);
        (await bartek.PutJsonAsync($"/api/v1/variants/{variant}/favorite", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await SetVisibilityAsync(ania, recipe, "private");
        (await ListAsync(bartek, "")).GetProperty("totalCount").GetInt32().ShouldBe(0);

        await SetVisibilityAsync(ania, recipe, "public");
        (await ListAsync(bartek, "")).GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task List_PagesAcrossBothKinds()
    {
        using var ania = await LoginAsync("ania");
        using var bartek = await LoginAsync("bartek");
        var expected = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var recipe = await CreateRecipeAsync(ania, $"Przepis {i}", "public");
            var variant = await AddVariantAsync(recipe, ania.UserId, $"Wariant {i}", VisibilityLevel.Public);
            await bartek.PutJsonAsync($"/api/v1/recipes/{recipe}/favorite", null);
            await Task.Delay(5, TestContext.Current.CancellationToken);
            await bartek.PutJsonAsync($"/api/v1/variants/{variant}/favorite", null);
            await Task.Delay(5, TestContext.Current.CancellationToken);
            expected.Insert(0, variant);
            expected.Insert(1, recipe);
        }

        var seen = new List<Guid>();
        foreach (var page in new[] { 1, 2, 3, 4 })
        {
            var result = await ListAsync(bartek, $"page={page}&pageSize=2");
            result.GetProperty("totalCount").GetInt32().ShouldBe(6);
            result.GetProperty("page").GetInt32().ShouldBe(page);
            foreach (var item in result.GetProperty("items").EnumerateArray())
            {
                seen.Add(item.GetProperty("kind").GetString() == "variant"
                    ? item.GetProperty("variant").GetProperty("id").GetGuid()
                    : item.GetProperty("recipe").GetProperty("id").GetGuid());
            }
        }

        seen.Count.ShouldBe(6);
        seen.ShouldBe(expected);
    }

    [Fact]
    public async Task List_InvalidPaging_Returns400()
    {
        using var ania = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(await ania.GetAsync("/api/v1/favorites?page=0"), HttpStatusCode.BadRequest, "validation.failed");
        await ContractAssert.IsProblemAsync(await ania.GetAsync("/api/v1/favorites?pageSize=101"), HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task EditDeletingVariants_RemovesTheirFavorites()
    {
        using var ania = await LoginAsync("ania");
        var created = await CreateAsync(ania, Draft("Zupa"));
        var recipe = created.GetProperty("id").GetGuid();
        var variant = await AddVariantAsync(recipe, ania.UserId, "Ostra");
        (await ania.PutJsonAsync($"/api/v1/variants/{variant}/favorite", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ListAsync(ania, "")).GetProperty("totalCount").GetInt32().ShouldBe(1);

        var update = await ania.PutJsonAsync($"/api/v1/recipes/{recipe}", UpdateBody(created, deleteVariants: true));

        update.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ListAsync(ania, "")).GetProperty("totalCount").GetInt32().ShouldBe(0);
        await using var db = Factory.CreateDbContext();
        (await db.VariantFavorites.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task DeletingARecipe_RemovesItsFavorites()
    {
        using var ania = await LoginAsync("ania");
        var recipe = await CreateRecipeAsync(ania, "Zupa", "private");
        await ania.PutJsonAsync($"/api/v1/recipes/{recipe}/favorite", null);

        (await ania.DeleteAsync($"/api/v1/recipes/{recipe}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await ListAsync(ania, "")).GetProperty("totalCount").GetInt32().ShouldBe(0);
        await using var db = Factory.CreateDbContext();
        (await db.RecipeFavorites.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    [Fact]
    public async Task ListKeys_RunsAConstantNumberOfStatementsWhateverThePageHolds()
    {
        using var ania = await LoginAsync("ania");
        for (var i = 0; i < 6; i++)
        {
            var recipe = await CreateRecipeAsync(ania, $"Przepis {i}", "private");
            var variant = await AddVariantAsync(recipe, ania.UserId, $"Wariant {i}");
            await ania.PutJsonAsync($"/api/v1/recipes/{recipe}/favorite", null);
            await ania.PutJsonAsync($"/api/v1/variants/{variant}/favorite", null);
        }

        var counts = new List<int>();
        foreach (var size in new[] { 2, 12 })
        {
            var counter = new StatementCounter();
            await using var db = Factory.CreateDbContext(counter);
            var user = Substitute.For<ICurrentUser>();
            user.Id.Returns(ania.UserId);
            var service = new FavoriteService(db, new VisibilityRules(db), user, TimeProvider.System);

            var page = await service.ListAsync(1, size, TestContext.Current.CancellationToken);

            page.Items.Count.ShouldBe(size);
            page.TotalCount.ShouldBe(12);
            counts.Add(counter.Count);
        }

        counts[0].ShouldBe(counts[1]);
        counts[0].ShouldBeLessThanOrEqualTo(2);
    }

    private async Task<Guid> CreateRecipeAsync(ApiClient client, string title, string visibility)
    {
        var body = Draft(title);
        body["visibility"] = visibility;
        return (await CreateAsync(client, body)).GetProperty("id").GetGuid();
    }

    private static async Task SetVisibilityAsync(ApiClient client, Guid recipeId, string visibility)
    {
        var response = await client.PutJsonAsync($"/api/v1/recipes/{recipeId}/visibility", new { visibility });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<JsonElement> ListAsync(ApiClient client, string query) =>
        await ContractAssert.IsJsonAsync(await client.GetAsync($"/api/v1/favorites?{query}"), HttpStatusCode.OK, "PagedFavoriteItem");

    private sealed class StatementCounter : DbCommandInterceptor
    {
        public int Count { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Count++;
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
