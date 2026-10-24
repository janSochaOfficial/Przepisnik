using System.Net;
using System.Text;
using System.Text.Json;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Fridges;

/// <summary>Shared arrangement for the fridge, deduction and shopping-list tests: a small catalog and request helpers.</summary>
public abstract class FridgeTestBase(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    protected ApiFactory Factory { get; } = factory;

    /// <summary>Volume, default package 1000 ml.</summary>
    protected Guid Milk { get; private set; }

    /// <summary>Volume, default package 330 ml.</summary>
    protected Guid Cream { get; private set; }

    /// <summary>Mass, no default package.</summary>
    protected Guid Flour { get; private set; }

    /// <summary>Count (pieces), default package 10.</summary>
    protected Guid Eggs { get; private set; }

    public async ValueTask InitializeAsync()
    {
        await Factory.ResetDatabaseAsync();
        await using var db = Factory.CreateDbContext();
        Milk = AddProduct(db, "Mleko", ProductCategory.Dairy, MeasureKind.Volume, DisplayClass.Liquid, 1000m);
        Cream = AddProduct(db, "Śmietanka 30%", ProductCategory.Dairy, MeasureKind.Volume, DisplayClass.Liquid, 330m);
        Flour = AddProduct(db, "Mąka pszenna", ProductCategory.DryGoods, MeasureKind.Mass, DisplayClass.Weight, null);
        Eggs = AddProduct(db, "Jajka", ProductCategory.Dairy, MeasureKind.Count, DisplayClass.Piece, 10m);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    protected async Task<ApiClient> LoginAsync(string nickname)
    {
        var client = Factory.CreateApiClient();
        await client.LoginAsync(nickname);
        return client;
    }

    /// <summary>The id of the user's automatically created "Moja lodówka".</summary>
    protected static async Task<Guid> DefaultFridgeAsync(ApiClient client) =>
        await DefaultFridgeOrNullAsync(client) ?? throw new InvalidOperationException("The user has no default fridge.");

    /// <summary>The user's current <c>settings.defaultFridgeId</c>.</summary>
    protected static async Task<Guid?> DefaultFridgeOrNullAsync(ApiClient client)
    {
        var me = await ApiClient.ReadJsonAsync(await client.GetAsync("/api/v1/me"));
        var id = me.GetProperty("settings").GetProperty("defaultFridgeId");
        return id.ValueKind == JsonValueKind.Null ? null : id.GetGuid();
    }

    protected static async Task<JsonElement> AddItemAsync(ApiClient client, Guid fridgeId, Guid productId, decimal packageAmount, decimal percent = 100m, string? note = null)
    {
        var response = await client.PostJsonAsync($"/api/v1/fridges/{fridgeId}/items", new { productId, packageAmount, remainingPercent = percent, note });
        var item = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "FridgeItem");
        response.Headers.Location.ShouldNotBeNull();
        return item;
    }

    protected static async Task<JsonElement> ItemsAsync(ApiClient client, Guid fridgeId)
    {
        return await IsJsonArrayAsync(await client.GetAsync($"/api/v1/fridges/{fridgeId}/items"), "FridgeItem");
    }

    protected static Task<HttpResponseMessage> PatchJsonAsync(ApiClient client, string path, object body) =>
        client.Http.PatchAsync(
            path,
            new StringContent(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

    /// <summary>Asserts a 200 JSON array whose every element matches <paramref name="schemaName"/>.</summary>
    protected static async Task<JsonElement> IsJsonArrayAsync(HttpResponseMessage response, string schemaName)
    {
        var body = await ApiClient.ReadJsonAsync(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        body.ValueKind.ShouldBe(JsonValueKind.Array);
        foreach (var element in body.EnumerateArray())
        {
            ContractAssert.MatchesSchema(element, schemaName);
        }

        return body;
    }

    private static Guid AddProduct(
        RecipesList.Infrastructure.Persistence.AppDbContext db,
        string name,
        ProductCategory category,
        MeasureKind kind,
        DisplayClass display,
        decimal? defaultPackage)
    {
        var product = new Product
        {
            Name = name,
            NormalizedName = NameNormalizer.Normalize(name),
            Category = category,
            MeasureKind = kind,
            DisplayClass = display,
            DefaultPackageAmount = defaultPackage,
            CreatedAt = TestData.Now,
        };
        db.Products.Add(product);
        return product.Id;
    }
}
