using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Catalog;

/// <summary>Search and idempotent create of tags and utensils (API.md §2, PROJECT §4.3).</summary>
public sealed class NameCatalogTests(ApiFactory factory) : CatalogTestBase(factory)
{
    private async Task AddTagsAsync(params (string Name, bool System)[] tags)
    {
        await using var db = Factory.CreateDbContext();
        db.Tags.AddRange(tags.Select(t => new Tag { Name = t.Name, NormalizedName = NameNormalizer.Normalize(t.Name), IsSystem = t.System }));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<JsonElement> ListAsync(ApiClient client, string path, string schema)
    {
        var response = await client.GetAsync(path);
        var body = await ApiClient.ReadJsonAsync(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        foreach (var item in body.EnumerateArray())
        {
            ContractAssert.MatchesSchema(item, schema);
        }

        return body;
    }

    [Fact]
    public async Task SearchTags_IsDiacriticInsensitive_AndRanksPrefixFirst()
    {
        await AddTagsAsync(("śniadanie", false), ("na śniadanie", false), ("obiad", false));
        using var client = await LoginAsync("ania");

        Names(await ListAsync(client, "/api/v1/tags?q=sniad", "TagRef")).ShouldBe(["śniadanie", "na śniadanie"]);
        Names(await ListAsync(client, "/api/v1/tags?limit=1", "TagRef")).Length.ShouldBe(1);
    }

    [Fact]
    public async Task CreateTag_New_Returns201_Existing_Returns200WithTheSameTag()
    {
        using var client = await LoginAsync("ania");

        var created = await client.PostJsonAsync("/api/v1/tags", new { name = "  Na  Imprezę " });
        var tag = await ContractAssert.IsJsonAsync(created, HttpStatusCode.Created, "TagRef");
        tag.GetProperty("name").GetString().ShouldBe("Na Imprezę");
        tag.GetProperty("isSystem").GetBoolean().ShouldBeFalse();
        created.Headers.Location.ShouldNotBeNull();

        var again = await ContractAssert.IsJsonAsync(
            await client.PostJsonAsync("/api/v1/tags", new { name = "na impreze" }), HttpStatusCode.OK, "TagRef");
        again.GetProperty("id").GetGuid().ShouldBe(tag.GetProperty("id").GetGuid());

        await using var db = Factory.CreateDbContext();
        (await db.Tags.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);
    }

    [Theory]
    [InlineData("szybkie")]
    [InlineData("  SZYBKIE ")]
    public async Task CreateTag_Reserved_Returns400_EvenWhenTheSystemTagExists(string name)
    {
        await AddTagsAsync(("szybkie", true));
        using var client = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/tags", new { name }), HttpStatusCode.BadRequest, "tag.reserved");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTag_BlankName_Returns400(string name)
    {
        using var client = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/tags", new { name }), HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Utensils_SearchAndIdempotentCreate()
    {
        using var client = await LoginAsync("ania");

        var created = await client.PostJsonAsync("/api/v1/utensils", new { name = "Blender" });
        var utensil = await ContractAssert.IsJsonAsync(created, HttpStatusCode.Created, "UtensilRef");
        var again = await ContractAssert.IsJsonAsync(
            await client.PostJsonAsync("/api/v1/utensils", new { name = " blender " }), HttpStatusCode.OK, "UtensilRef");
        again.GetProperty("id").GetGuid().ShouldBe(utensil.GetProperty("id").GetGuid());
        await client.PostJsonAsync("/api/v1/utensils", new { name = "Łyżka drewniana" });

        Names(await ListAsync(client, "/api/v1/utensils?q=lyzka", "UtensilRef")).ShouldBe(["Łyżka drewniana"]);
        Names(await ListAsync(client, "/api/v1/utensils", "UtensilRef")).Length.ShouldBe(2);
    }
}
