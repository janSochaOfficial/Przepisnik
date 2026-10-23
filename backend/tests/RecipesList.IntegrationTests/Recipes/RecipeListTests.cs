using System.Data.Common;
using System.Net;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;
using RecipesList.Api.Features.Recipes;
using RecipesList.Core.Common;
using RecipesList.Core.Favorites;
using RecipesList.Core.Visibility;
using RecipesList.IntegrationTests.Images;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Recipes;

/// <summary>GET /recipes: filters, paging, favourites without N+1, and the image-ownership rule.</summary>
public sealed class RecipeListTests(ApiFactory factory) : RecipeTestBase(factory)
{
    [Fact]
    public async Task List_TagsFilter_RequiresEveryTag()
    {
        using var ola = await LoginAsync("ola");
        await CreateWithTagsAsync(ola, "Tylko obiad", tags: [Dinner]);
        await CreateWithTagsAsync(ola, "Obiad wege", tags: [Dinner, Vegan]);
        await CreateWithTagsAsync(ola, "Bez tagów");

        var both = await ListAsync(ola, $"tags={Dinner},{Vegan}");
        var dinner = await ListAsync(ola, $"tags={Dinner}");

        Titles(both).ShouldBe(["Obiad wege"]);
        Titles(dinner).ToHashSet().ShouldBe(["Tylko obiad", "Obiad wege"], ignoreOrder: true);
        both.GetProperty("totalCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task List_Paging_ReturnsPagesAndTheTotal()
    {
        using var ola = await LoginAsync("ola");
        for (var i = 1; i <= 5; i++)
        {
            await CreateWithTagsAsync(ola, $"Przepis {i}");
        }

        var first = await ListAsync(ola, "page=1&pageSize=2&sort=title");
        var third = await ListAsync(ola, "page=3&pageSize=2&sort=title");

        Titles(first).ShouldBe(["Przepis 1", "Przepis 2"]);
        Titles(third).ShouldBe(["Przepis 5"]);
        first.GetProperty("totalCount").GetInt32().ShouldBe(5);
        first.GetProperty("page").GetInt32().ShouldBe(1);
        first.GetProperty("pageSize").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task List_DefaultSort_IsNewestFirst()
    {
        using var ola = await LoginAsync("ola");
        await CreateWithTagsAsync(ola, "Stary");
        await CreateWithTagsAsync(ola, "Nowy");

        Titles(await ListAsync(ola, "")).ShouldBe(["Nowy", "Stary"]);
    }

    [Fact]
    public async Task List_SearchIgnoresCaseAndDiacritics()
    {
        using var ola = await LoginAsync("ola");
        await CreateWithTagsAsync(ola, "Żurek staropolski");
        await CreateWithTagsAsync(ola, "Naleśniki");

        Titles(await ListAsync(ola, "q=ZUREK")).ShouldBe(["Żurek staropolski"]);
        Titles(await ListAsync(ola, "q=nalesn")).ShouldBe(["Naleśniki"]);
        Titles(await ListAsync(ola, "q=100%25")).ShouldBeEmpty();
    }

    [Fact]
    public async Task List_AuthorMe_ReturnsOnlyOwnRecipes()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        await CreateWithTagsAsync(ola, "Olina", visibility: "public");
        await CreateWithTagsAsync(kasia, "Kasina", visibility: "public");

        Titles(await ListAsync(ola, "author=me")).ShouldBe(["Olina"]);
        Titles(await ListAsync(ola, $"author={kasia.UserId}")).ShouldBe(["Kasina"]);
        Titles(await ListAsync(ola, "visibility=public")).ToHashSet().ShouldBe(["Olina", "Kasina"], ignoreOrder: true);
    }

    [Fact]
    public async Task List_InvalidFilters_Return400()
    {
        using var ola = await LoginAsync("ola");

        await ContractAssert.IsProblemAsync(await ola.GetAsync("/api/v1/recipes?tags=nope"), HttpStatusCode.BadRequest, "validation.failed");
        await ContractAssert.IsProblemAsync(await ola.GetAsync("/api/v1/recipes?pageSize=1000"), HttpStatusCode.BadRequest, "validation.failed");
        await ContractAssert.IsProblemAsync(await ola.GetAsync("/api/v1/recipes?sort=random"), HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task List_ReportsFavouritesTagsAndVariantCountsPerItem()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var liked = await CreateWithTagsAsync(ola, "Lubiany", visibility: "public", tags: [Dinner]);
        await CreateWithTagsAsync(ola, "Obojętny", visibility: "public");
        await AddVariantAsync(liked, kasia.UserId, "Wariant", VisibilityLevel.Public);
        await using (var db = Factory.CreateDbContext())
        {
            db.RecipeFavorites.Add(new RecipeFavorite { UserId = kasia.UserId, RecipeId = liked, CreatedAt = TestData.Now });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var forKasia = (await ListAsync(kasia, "sort=title")).GetProperty("items").EnumerateArray().ToList();
        var forOla = (await ListAsync(ola, "sort=title")).GetProperty("items").EnumerateArray().ToList();

        forKasia[0].GetProperty("title").GetString().ShouldBe("Lubiany");
        forKasia[0].GetProperty("isFavorite").GetBoolean().ShouldBeTrue();
        forKasia[1].GetProperty("isFavorite").GetBoolean().ShouldBeFalse();
        forKasia[0].GetProperty("variantCount").GetInt32().ShouldBe(1);
        forKasia[0].GetProperty("tags")[0].GetProperty("name").GetString().ShouldBe("obiad");
        forOla[0].GetProperty("isFavorite").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task List_NumberOfSqlStatements_DoesNotDependOnThePageSize()
    {
        using var ola = await LoginAsync("ola");
        for (var i = 0; i < 12; i++)
        {
            await CreateWithTagsAsync(ola, $"Przepis {i:00}", tags: [Dinner, Vegan]);
        }

        var small = await CountStatementsAsync(ola.UserId, pageSize: 2);
        var large = await CountStatementsAsync(ola.UserId, pageSize: 12);

        small.ShouldBe(large);
        large.ShouldBeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Create_WithSomeoneElsesImage_Returns400NotOwned_AndWithAnOwnImageSucceeds()
    {
        using var ola = await LoginAsync("ola");
        using var kasia = await LoginAsync("kasia");
        var mine = await UploadAsync(ola);
        var hers = await UploadAsync(kasia);

        var bad = Draft();
        bad["coverImageId"] = hers;
        bad["instructionSteps"] = new object[] { new { id = (Guid?)null, blocks = new object[] { new { type = "image", imageId = hers, caption = "x" } } } };
        var problem = await ContractAssert.IsProblemAsync(await ola.PostJsonAsync("/api/v1/recipes", bad), HttpStatusCode.BadRequest, "image.not_owned");
        problem.GetProperty("errors").TryGetProperty("coverImageId", out _).ShouldBeTrue();
        problem.GetProperty("errors").TryGetProperty("instructionSteps[0].blocks[0].imageId", out _).ShouldBeTrue();

        var good = Draft();
        good["coverImageId"] = mine;
        good["instructionSteps"] = new object[] { new { id = (Guid?)null, blocks = new object[] { new { type = "text", text = "a" }, new { type = "image", imageId = mine, caption = (string?)null } } } };
        var created = await CreateAsync(ola, good);

        created.GetProperty("coverImage").GetProperty("id").GetGuid().ShouldBe(mine);
        created.GetProperty("coverImage").GetProperty("thumbnailUrl").GetString()!.ShouldContain("size=thumb");
        created.GetProperty("instructionSteps")[0].GetProperty("blocks")[1].GetProperty("imageId").GetGuid().ShouldBe(mine);
    }

    [Fact]
    public async Task Update_KeepingAnImageTheRecipeAlreadyUses_IsAllowed()
    {
        using var ola = await LoginAsync("ola");
        var mine = await UploadAsync(ola);
        var body = Draft();
        body["coverImageId"] = mine;
        var created = await CreateAsync(ola, body);

        var response = await ola.PutJsonAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}", UpdateBody(created));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<int> CountStatementsAsync(Guid userId, int pageSize)
    {
        var counter = new StatementCounter();
        await using var db = Factory.CreateDbContext(counter);
        var user = Substitute.For<ICurrentUser>();
        user.Id.Returns(userId);
        var queries = new RecipeQueries(db, new VisibilityRules(db), user);

        var page = await queries.ListAsync(
            new RecipeListQuery(null, [Dinner, Vegan], null, null, 1, pageSize, "newest"),
            TestContext.Current.CancellationToken);

        page.Items.Count.ShouldBe(pageSize);
        return counter.Count;
    }

    private static async Task<Guid> UploadAsync(ApiClient client)
    {
        var file = new ByteArrayContent(ImageFixtures.Png(30, 30));
        var content = new MultipartFormDataContent { { file, "file", "a.png" } };
        var response = await client.Http.PostAsync("/api/v1/images", content, TestContext.Current.CancellationToken);
        var info = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "ImageInfo");
        return info.GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateWithTagsAsync(ApiClient client, string title, string visibility = "private", Guid[]? tags = null)
    {
        var body = Draft(title);
        body["tagIds"] = tags ?? [];
        body["visibility"] = visibility;
        return (await CreateAsync(client, body)).GetProperty("id").GetGuid();
    }

    private static async Task<System.Text.Json.JsonElement> ListAsync(ApiClient client, string query) =>
        await ContractAssert.IsJsonAsync(await client.GetAsync($"/api/v1/recipes?{query}"), HttpStatusCode.OK, "PagedRecipeSummary");

    private static string[] Titles(System.Text.Json.JsonElement page) =>
        [.. page.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("title").GetString()!)];

    private sealed class StatementCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
