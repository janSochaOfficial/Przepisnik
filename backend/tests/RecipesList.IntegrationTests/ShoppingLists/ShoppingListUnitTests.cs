using System.Net;
using System.Text.Json;
using RecipesList.Core.Catalog;
using RecipesList.IntegrationTests.Infrastructure;
using RecipesList.IntegrationTests.Units;

namespace RecipesList.IntegrationTests.ShoppingLists;

/// <summary>Units on shopping-list items (PROJECT §10, D-34, API.md §7).</summary>
public sealed class ShoppingListUnitTests(ApiFactory factory) : UnitTestBase(factory)
{
    [Fact]
    public async Task Add_WithAQuantity_KeepsTheUnitAndDerivesTheBaseAmount()
    {
        var milk = await AddFoodAsync("Mleko", MeasureKind.Volume, gramsPerMl: 1.03m);
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);

        var added = await AddAsync(anna, fridgeId, WithQuantity(milk, Builtin(1.5m, "l")));

        added[0].GetProperty("amount").GetDecimal().ShouldBe(1500m);
        added[0].GetProperty("quantity").GetProperty("unit").GetString().ShouldBe("l");
        added[0].GetProperty("quantity").GetProperty("value").GetDecimal().ShouldBe(1.5m);
        (await ListAsync(anna, fridgeId))[0].GetProperty("quantity").GetProperty("value").GetDecimal().ShouldBe(1.5m);
    }

    [Fact]
    public async Task Patch_Quantity_ChangesTheUnit_AmountDropsIt_ClearAmountRemovesBoth()
    {
        var milk = await AddFoodAsync("Mleko", MeasureKind.Volume, gramsPerMl: 1.03m);
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var entry = (await AddAsync(anna, fridgeId, WithQuantity(milk, Builtin(1.5m, "l"))))[0];

        var inMl = await PatchAsync(anna, fridgeId, entry, new { version = Version(entry), quantity = Builtin(1500m, "ml") });
        inMl.GetProperty("amount").GetDecimal().ShouldBe(1500m);
        inMl.GetProperty("quantity").GetProperty("unit").GetString().ShouldBe("ml");

        var inGlasses = await PatchAsync(anna, fridgeId, inMl, new { version = Version(inMl), quantity = Builtin(6m, "glass") });
        inGlasses.GetProperty("amount").GetDecimal().ShouldBe(1500m);

        var baseOnly = await PatchAsync(anna, fridgeId, inGlasses, new { version = Version(inGlasses), amount = 700m });
        baseOnly.GetProperty("amount").GetDecimal().ShouldBe(700m);
        baseOnly.GetProperty("quantity").ValueKind.ShouldBe(JsonValueKind.Null);

        var withUnit = await PatchAsync(anna, fridgeId, baseOnly, new { version = Version(baseOnly), quantity = Builtin(2m, "glass") });
        var cleared = await PatchAsync(anna, fridgeId, withUnit, new { version = Version(withUnit), clearAmount = true });
        cleared.GetProperty("amount").ValueKind.ShouldBe(JsonValueKind.Null);
        cleared.GetProperty("quantity").ValueKind.ShouldBe(JsonValueKind.Null);
        var reloaded = (await ListAsync(anna, fridgeId))[0];
        reloaded.GetProperty("amount").ValueKind.ShouldBe(JsonValueKind.Null);
        reloaded.GetProperty("quantity").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Invalid_QuantityCombinations_Return400()
    {
        var milk = await AddFoodAsync("Mleko", MeasureKind.Volume);
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var entry = (await AddAsync(anna, fridgeId, WithAmount(milk, 100m), Text("Chleb")))[0];
        var bread = (await ListAsync(anna, fridgeId)).EnumerateArray().Single(e => e.GetProperty("text").ValueKind == JsonValueKind.String);

        // Mass unit without a density does not fit a volume product.
        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync(ItemsPath(fridgeId), new { items = new[] { WithQuantity(milk, Builtin(200m, "g")) } }),
            HttpStatusCode.BadRequest,
            "quantity.unit_incompatible");
        await ContractAssert.IsProblemAsync(
            await anna.PatchJsonAsync(ItemPath(fridgeId, entry), new { version = Version(entry), quantity = Builtin(1m, "kg") }),
            HttpStatusCode.BadRequest,
            "quantity.unit_incompatible");
        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync(ItemsPath(fridgeId), new { items = new[] { new { productId = (Guid?)milk, text = (string?)null, amount = (decimal?)100m, quantity = Builtin(1m, "l") } } }),
            HttpStatusCode.BadRequest,
            "shopping_item.invalid");
        await ContractAssert.IsProblemAsync(
            await anna.PatchJsonAsync(ItemPath(fridgeId, entry), new { version = Version(entry), amount = 100m, clearAmount = true }),
            HttpStatusCode.BadRequest,
            "shopping_item.invalid");
        await ContractAssert.IsProblemAsync(
            await anna.PatchJsonAsync(ItemPath(fridgeId, bread), new { version = Version(bread), quantity = Builtin(1m, "l") }),
            HttpStatusCode.BadRequest,
            "shopping_item.invalid");
    }

    [Fact]
    public async Task Merge_ExistingWithoutUnit_TakesTheNewUnit()
    {
        var milk = await AddFoodAsync("Mleko", MeasureKind.Volume);
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddAsync(anna, fridgeId, WithAmount(milk, 300m));

        var merged = (await AddAsync(anna, fridgeId, WithQuantity(milk, Builtin(1m, "l"))))[0];

        merged.GetProperty("amount").GetDecimal().ShouldBe(1300m);
        merged.GetProperty("quantity").GetProperty("unit").GetString().ShouldBe("l");
        merged.GetProperty("quantity").GetProperty("value").GetDecimal().ShouldBe(1.3m);
    }

    [Fact]
    public async Task Merge_ExistingUnitIsKept_AndNeitherUnitMeansNone()
    {
        var milk = await AddFoodAsync("Mleko", MeasureKind.Volume);
        var water = await AddFoodAsync("Woda", MeasureKind.Volume);
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddAsync(anna, fridgeId, WithQuantity(milk, Builtin(2m, "glass")), WithAmount(water, 100m));

        var merged = await AddAsync(anna, fridgeId, WithQuantity(milk, Builtin(100m, "ml")), WithAmount(water, 50m));

        merged[0].GetProperty("amount").GetDecimal().ShouldBe(600m);
        merged[0].GetProperty("quantity").GetProperty("unit").GetString().ShouldBe("glass");
        merged[0].GetProperty("quantity").GetProperty("value").GetDecimal().ShouldBe(2.4m);
        merged[1].GetProperty("amount").GetDecimal().ShouldBe(150m);
        merged[1].GetProperty("quantity").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Merge_CustomUnit_IsExpressedExactlyAndEmbedsTheUnit()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var canId = (await CreateUnitAsync(anna, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var first = (await AddAsync(anna, fridgeId, WithQuantity(tomatoes, Custom(2m, canId))))[0];
        first.GetProperty("amount").GetDecimal().ShouldBe(800m);
        first.GetProperty("quantity").GetProperty("customUnit").GetProperty("name").GetString().ShouldBe("puszka");

        var merged = (await AddAsync(anna, fridgeId, WithQuantity(tomatoes, Builtin(200m, "g"))))[0];

        merged.GetProperty("amount").GetDecimal().ShouldBe(1000m);
        merged.GetProperty("quantity").GetProperty("value").GetDecimal().ShouldBe(2.5m);
        merged.GetProperty("quantity").GetProperty("customUnitId").GetGuid().ShouldBe(canId);
        merged.GetProperty("quantity").GetProperty("customUnit").GetProperty("amount").GetDecimal().ShouldBe(400m);
    }

    [Fact]
    public async Task CustomUnitOfAShoppingItem_CannotBeRedefined()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var admin = await LoginAdminAsync();
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var canId = (await CreateUnitAsync(anna, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        await AddAsync(anna, fridgeId, WithQuantity(tomatoes, Custom(2m, canId)));

        await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/units/{canId}", UnitBody("puszka", CanForms, tomatoes, 500, "g")),
            HttpStatusCode.BadRequest,
            "unit.in_use");
    }

    private static object WithQuantity(Guid productId, object quantity) =>
        new { productId = (Guid?)productId, text = (string?)null, amount = (decimal?)null, quantity };

    private static object WithAmount(Guid productId, decimal amount) =>
        new { productId = (Guid?)productId, text = (string?)null, amount = (decimal?)amount };

    private static object Text(string text) => new { productId = (Guid?)null, text = (string?)text, amount = (decimal?)null };

    private static string ItemsPath(Guid fridgeId) => $"/api/v1/fridges/{fridgeId}/shopping-list/items";

    private static string ItemPath(Guid fridgeId, JsonElement entry) => $"{ItemsPath(fridgeId)}/{entry.GetProperty("id").GetGuid()}";

    private static uint Version(JsonElement entry) => entry.GetProperty("version").GetUInt32();

    private static async Task<Guid> DefaultFridgeAsync(ApiClient client)
    {
        var me = await ApiClient.ReadJsonAsync(await client.GetAsync("/api/v1/me"));
        return me.GetProperty("settings").GetProperty("defaultFridgeId").GetGuid();
    }

    private static async Task<JsonElement> AddAsync(ApiClient client, Guid fridgeId, params object[] items) =>
        await IsItemArrayAsync(await client.PostJsonAsync(ItemsPath(fridgeId), new { items }));

    private static async Task<JsonElement> ListAsync(ApiClient client, Guid fridgeId) =>
        await IsItemArrayAsync(await client.GetAsync($"/api/v1/fridges/{fridgeId}/shopping-list"));

    private static async Task<JsonElement> IsItemArrayAsync(HttpResponseMessage response)
    {
        var body = await ApiClient.ReadJsonAsync(response);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, body.ToString());
        foreach (var element in body.EnumerateArray())
        {
            ContractAssert.MatchesSchema(element, "ShoppingListItem");
        }

        return body;
    }

    private static async Task<JsonElement> PatchAsync(ApiClient client, Guid fridgeId, JsonElement entry, object body) =>
        await ContractAssert.IsJsonAsync(await client.PatchJsonAsync(ItemPath(fridgeId, entry), body), HttpStatusCode.OK, "ShoppingListItem");
}
