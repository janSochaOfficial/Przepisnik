using System.Net;
using System.Text.Json;
using RecipesList.Core.Catalog;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Catalog;

/// <summary>Search, create and dedupe of products (API.md §2, PROJECT §7.1).</summary>
public sealed class ProductEndpointsTests(ApiFactory factory) : CatalogTestBase(factory)
{
    private static async Task<JsonElement> SearchAsync(ApiClient client, string query)
    {
        var response = await client.GetAsync("/api/v1/products" + query);
        var body = await ApiClient.ReadJsonAsync(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        foreach (var item in body.EnumerateArray())
        {
            ContractAssert.MatchesSchema(item, "ProductRef");
        }

        return body;
    }

    [Fact]
    public async Task Search_Smiet_FindsBothCreamsEvenWithoutDiacritics()
    {
        await AddProductAsync("Śmietanka 30%", MeasureKind.Volume, DisplayClass.Liquid, ProductCategory.Dairy);
        await AddProductAsync("Śmietana 18%", MeasureKind.Volume, DisplayClass.Liquid, ProductCategory.Dairy);
        await AddProductAsync("Mleko", MeasureKind.Volume, DisplayClass.Liquid, ProductCategory.Dairy);
        using var client = await LoginAsync("ania");

        var names = Names(await SearchAsync(client, "?q=smiet"));

        names.ShouldBe(["Śmietana 18%", "Śmietanka 30%"], ignoreOrder: true);
    }

    [Fact]
    public async Task Search_RanksPrefixMatchesAboveWordPrefixAndSimilarity()
    {
        await AddProductAsync("Kasza jęczmienna");
        await AddProductAsync("Mąka tortowa");
        await AddProductAsync("Mąka");
        await AddProductAsync("Maka kukurydziana");
        await AddProductAsync("Mak niebieski");
        await AddProductAsync("Tortowa maka");
        using var client = await LoginAsync("ania");

        var names = Names(await SearchAsync(client, "?q=maka"));

        // Prefix matches first (alphabetical among equals), then the word-prefix match, then the fuzzy ones.
        names.Take(3).ShouldBe(["Maka kukurydziana", "Mąka", "Mąka tortowa"], ignoreOrder: true);
        names[3].ShouldBe("Tortowa maka");
        names.ShouldNotContain("Kasza jęczmienna");
    }

    [Fact]
    public async Task Search_FindsFuzzyTypos()
    {
        await AddProductAsync("Pomidor");
        await AddProductAsync("Ogórek");
        using var client = await LoginAsync("ania");

        var names = Names(await SearchAsync(client, "?q=pomidro"));

        names.ShouldBe(["Pomidor"]);
    }

    [Fact]
    public async Task Search_TreatsLikeWildcardsAsText()
    {
        await AddProductAsync("Cebula");
        await AddProductAsync("Śmietana 18%");
        using var client = await LoginAsync("ania");

        Names(await SearchAsync(client, "?q=%25")).ShouldBeEmpty();
        Names(await SearchAsync(client, "?q=18%25")).ShouldBe(["Śmietana 18%"]);
    }

    [Fact]
    public async Task Search_FiltersByCategory_AndCapsTheLimit()
    {
        await AddProductAsync("Marchew", category: ProductCategory.Vegetables);
        await AddProductAsync("Mięta", category: ProductCategory.Spices);
        for (var i = 0; i < 55; i++)
        {
            await AddProductAsync($"Produkt {i:00}");
        }

        using var client = await LoginAsync("ania");

        Names(await SearchAsync(client, "?category=vegetables")).ShouldBe(["Marchew"]);
        (await SearchAsync(client, "")).GetArrayLength().ShouldBe(20);
        (await SearchAsync(client, "?limit=50")).GetArrayLength().ShouldBe(50);
        (await SearchAsync(client, "?limit=500")).GetArrayLength().ShouldBe(50);
        (await SearchAsync(client, "?limit=3")).GetArrayLength().ShouldBe(3);
    }

    [Fact]
    public async Task Get_ReturnsTheProduct_Or404()
    {
        var id = await AddProductAsync("Cebula");
        using var client = await LoginAsync("ania");

        var product = await ContractAssert.IsJsonAsync(await client.GetAsync($"/api/v1/products/{id}"), HttpStatusCode.OK, "ProductRef");
        product.GetProperty("name").GetString().ShouldBe("Cebula");

        await ContractAssert.IsProblemAsync(await client.GetAsync($"/api/v1/products/{Guid.NewGuid()}"), HttpStatusCode.NotFound, "product.not_found");
    }

    [Fact]
    public async Task Create_StoresTheProductWithItsCreator()
    {
        using var client = await LoginAsync("ania");
        var body = ProductBody("  Czosnek   niedźwiedzi ", "vegetables", "count", "piece", hasSizes: true, allowHalf: true);
        body["gramsPerPiece"] = 40;
        body["pieceForms"] = new { one = "ząbek", few = "ząbki", many = "ząbków", fraction = "ząbka" };

        var response = await client.PostJsonAsync("/api/v1/products", body);
        var product = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "ProductRef");

        product.GetProperty("name").GetString().ShouldBe("Czosnek niedźwiedzi");
        product.GetProperty("pieceForms").GetProperty("few").GetString().ShouldBe("ząbki");
        response.Headers.Location!.ToString().ShouldEndWith($"/api/v1/products/{product.GetProperty("id").GetGuid()}");

        await using var db = Factory.CreateDbContext();
        var stored = await db.Products.FindAsync([product.GetProperty("id").GetGuid()], TestContext.Current.CancellationToken);
        stored!.NormalizedName.ShouldBe("czosnek niedzwiedzi");
        stored.CreatedById.ShouldBe(client.UserId);
    }

    [Theory]
    [InlineData("  Cebula ")]
    [InlineData("cebula")]
    [InlineData("CEBULA")]
    public async Task Create_Duplicate_Returns409WithTheExistingId(string name)
    {
        var existing = await AddProductAsync("Cebula");
        using var client = await LoginAsync("ania");

        var problem = await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/products", ProductBody(name)), HttpStatusCode.Conflict, "product.duplicate");

        problem.GetProperty("existingId").GetGuid().ShouldBe(existing);
    }

    [Fact]
    public async Task Create_DiacriticsDuplicate_IsBlocked()
    {
        var existing = await AddProductAsync("Śmietana 18%", MeasureKind.Volume, DisplayClass.Liquid);
        using var client = await LoginAsync("ania");

        var problem = await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/products", ProductBody("smietana 18%", measureKind: "volume", displayClass: "liquid")),
            HttpStatusCode.Conflict,
            "product.duplicate");

        problem.GetProperty("existingId").GetGuid().ShouldBe(existing);
    }

    [Theory]
    [InlineData("mass", "liquid", false, false)]
    [InlineData("volume", "weight", false, false)]
    [InlineData("count", "weight", false, false)]
    [InlineData("mass", "weight", true, false)]
    [InlineData("volume", "liquid", false, true)]
    public async Task Create_InvalidCombination_Returns400(string measureKind, string displayClass, bool hasSizes, bool allowHalf)
    {
        using var client = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/products", ProductBody("Coś", "other", measureKind, displayClass, hasSizes, allowHalf)),
            HttpStatusCode.BadRequest,
            "product.invalid_combination");
    }

    [Theory]
    [InlineData("a")]
    [InlineData("   ")]
    public async Task Create_BadName_Returns400(string name)
    {
        using var client = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/products", ProductBody(name)), HttpStatusCode.BadRequest, "validation.failed");
    }

    [Fact]
    public async Task Create_NonPositiveDensity_Returns400()
    {
        using var client = await LoginAsync("ania");
        var body = ProductBody("Olej", "oilsVinegars", "volume", "liquid");
        body["gramsPerMl"] = 0;

        var problem = await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/products", body), HttpStatusCode.BadRequest, "validation.failed");

        problem.GetProperty("errors").GetProperty("gramsPerMl")[0].GetString().ShouldBe("must_be_positive");
    }

    [Fact]
    public async Task Create_WithoutSession_Returns401()
    {
        using var client = Factory.CreateApiClient();
        client.EnableCsrfHeader();

        await ContractAssert.IsProblemAsync(
            await client.PostJsonAsync("/api/v1/products", ProductBody("Cebula")), HttpStatusCode.Unauthorized, "auth.required");
    }
}
