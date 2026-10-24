using System.Net;
using System.Text.Json;
using RecipesList.IntegrationTests.Fridges;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.ShoppingLists;

/// <summary>The per-fridge shopping list (API.md §7, PROJECT §10).</summary>
public sealed class ShoppingListTests(ApiFactory factory) : FridgeTestBase(factory)
{
    [Fact]
    public async Task Add_SameProductTwice_MergesAmounts()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);

        var first = await AddAsync(anna, fridgeId, Product(Milk, 500m));
        var second = await AddAsync(anna, fridgeId, Product(Milk, 500m));

        second[0].GetProperty("id").GetGuid().ShouldBe(first[0].GetProperty("id").GetGuid());
        second[0].GetProperty("amount").GetDecimal().ShouldBe(1000m);
        var list = await ListAsync(anna, fridgeId);
        list.EnumerateArray().ShouldHaveSingleItem().GetProperty("amount").GetDecimal().ShouldBe(1000m);
    }

    [Fact]
    public async Task Add_NullPlusAmountAndDuplicatesInOneBatch_MergeIntoOneEntry()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddAsync(anna, fridgeId, Product(Cream, null));

        var result = await AddAsync(anna, fridgeId, Product(Cream, 200m), Product(Cream, 100m), Text("Chleb"), Text("Chleb"));

        result.GetArrayLength().ShouldBe(3);
        result[0].GetProperty("amount").GetDecimal().ShouldBe(300m);
        result[1].GetProperty("text").GetString().ShouldBe("Chleb");
        result[2].GetProperty("text").GetString().ShouldBe("Chleb");
    }

    [Fact]
    public async Task Add_ProductWhoseEntryIsChecked_CreatesANewEntry()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var milk = (await AddAsync(anna, fridgeId, Product(Milk, 500m)))[0];
        await PatchAsync(anna, fridgeId, milk, new { version = Version(milk), @checked = true, amount = (decimal?)null, text = (string?)null });

        var again = await AddAsync(anna, fridgeId, Product(Milk, 250m));

        again[0].GetProperty("id").GetGuid().ShouldNotBe(milk.GetProperty("id").GetGuid());
        again[0].GetProperty("amount").GetDecimal().ShouldBe(250m);
    }

    [Fact]
    public async Task Add_InvalidEntries_Returns400ShoppingItemInvalid()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);

        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/shopping-list/items", new { items = new[] { new { productId = (Guid?)Milk, text = (string?)"Mleko", amount = (decimal?)null } } }),
            HttpStatusCode.BadRequest,
            "shopping_item.invalid");
        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/shopping-list/items", new { items = new[] { new { productId = (Guid?)null, text = (string?)"Chleb", amount = (decimal?)2m } } }),
            HttpStatusCode.BadRequest,
            "shopping_item.invalid");
        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/shopping-list/items", new { items = new[] { new { productId = (Guid?)null, text = (string?)"  ", amount = (decimal?)null } } }),
            HttpStatusCode.BadRequest,
            "shopping_item.invalid");
        await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/shopping-list/items", new { items = Array.Empty<object>() }),
            HttpStatusCode.BadRequest,
            "validation.failed");
    }

    [Fact]
    public async Task List_UncheckedFirstThenCategoryThenName()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var added = await AddAsync(anna, fridgeId, Product(Flour, null), Text("Zapałki"), Product(Milk, null), Product(Eggs, 10m), Product(Cream, null));
        var cream = added[4];
        await PatchAsync(anna, fridgeId, cream, new { version = Version(cream), @checked = true, amount = (decimal?)null, text = (string?)null });

        var list = await ListAsync(anna, fridgeId);

        list.EnumerateArray().Select(Name).ShouldBe(["Jajka", "Mleko", "Mąka pszenna", "Zapałki", "Śmietanka 30%"]);
    }

    [Fact]
    public async Task Patch_StaleVersion_Returns409AndWrongFieldReturns400()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var milk = (await AddAsync(anna, fridgeId, Product(Milk, 500m)))[0];

        var updated = await PatchAsync(anna, fridgeId, milk, new { version = Version(milk), @checked = (bool?)null, amount = 750m, text = (string?)null });
        updated.GetProperty("amount").GetDecimal().ShouldBe(750m);
        updated.GetProperty("checked").GetBoolean().ShouldBeFalse();

        await ContractAssert.IsProblemAsync(
            await PatchJsonAsync(anna, ItemPath(fridgeId, milk), new { version = Version(milk), @checked = true, amount = (decimal?)null, text = (string?)null }),
            HttpStatusCode.Conflict,
            "concurrency.conflict");
        await ContractAssert.IsProblemAsync(
            await PatchJsonAsync(anna, ItemPath(fridgeId, milk), new { version = Version(updated), @checked = (bool?)null, amount = (decimal?)null, text = "Mleko" }),
            HttpStatusCode.BadRequest,
            "shopping_item.invalid");
    }

    [Fact]
    public async Task Delete_RemovesTheEntry_UnknownReturns404()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var bread = (await AddAsync(anna, fridgeId, Text("Chleb")))[0];

        (await anna.DeleteAsync(ItemPath(fridgeId, bread))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ContractAssert.IsProblemAsync(await anna.DeleteAsync(ItemPath(fridgeId, bread)), HttpStatusCode.NotFound, "shopping_item.not_found");
    }

    [Fact]
    public async Task Complete_MoveToFridge_CreatesFullPackagesAndRemovesCheckedEntries()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var added = await AddAsync(anna, fridgeId, Product(Milk, 500m), Product(Cream, null), Text("Chleb"), Product(Flour, null), Product(Eggs, 6m));
        foreach (var entry in added.EnumerateArray().Take(4))
        {
            await PatchAsync(anna, fridgeId, entry, new { version = Version(entry), @checked = true, amount = (decimal?)null, text = (string?)null });
        }

        var response = await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/shopping-list/complete", new { moveToFridge = true });
        var result = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "CompleteShoppingListResult");

        // Milk (500 ml as listed), cream (the 330 ml product default) and bread (text, just removed); flour has neither
        // an amount nor a default package, so it stays on the list; the unchecked eggs stay too.
        result.GetProperty("removed").GetInt32().ShouldBe(3);
        result.GetProperty("createdFridgeItems").EnumerateArray()
            .Select(i => (i.GetProperty("product").GetProperty("name").GetString(), i.GetProperty("packageAmount").GetDecimal(), i.GetProperty("remainingPercent").GetDecimal()))
            .ShouldBe([("Mleko", 500m, 100m), ("Śmietanka 30%", 330m, 100m)]);
        (await ItemsAsync(anna, fridgeId)).GetArrayLength().ShouldBe(2);
        (await ListAsync(anna, fridgeId)).EnumerateArray().Select(Name).ShouldBe(["Jajka", "Mąka pszenna"]);
    }

    [Fact]
    public async Task Complete_WithoutMove_OnlyRemovesCheckedEntries()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var milk = (await AddAsync(anna, fridgeId, Product(Milk, 500m), Text("Chleb")))[0];
        await PatchAsync(anna, fridgeId, milk, new { version = Version(milk), @checked = true, amount = (decimal?)null, text = (string?)null });

        var result = await ContractAssert.IsJsonAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/shopping-list/complete", new { moveToFridge = false }),
            HttpStatusCode.OK,
            "CompleteShoppingListResult");

        result.GetProperty("removed").GetInt32().ShouldBe(1);
        result.GetProperty("createdFridgeItems").GetArrayLength().ShouldBe(0);
        (await ItemsAsync(anna, fridgeId)).GetArrayLength().ShouldBe(0);
        (await ListAsync(anna, fridgeId)).EnumerateArray().Select(Name).ShouldBe(["Chleb"]);
    }

    [Fact]
    public async Task List_SharedFridge_MembersSeeTheSameList()
    {
        using var anna = await LoginAsync("anna");
        using var bartek = await LoginAsync("bartek");
        var fridgeId = await DefaultFridgeAsync(anna);
        var invite = await ApiClient.ReadJsonAsync(await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/invites", null));
        (await bartek.PostJsonAsync($"/api/v1/invites/{invite.GetProperty("token").GetString()}/accept", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        await AddAsync(bartek, fridgeId, Product(Milk, 1000m));

        var entry = (await ListAsync(anna, fridgeId)).EnumerateArray().ShouldHaveSingleItem();
        entry.GetProperty("addedBy").GetProperty("nickname").GetString().ShouldBe("bartek");
    }

    private static object Product(Guid productId, decimal? amount) => new { productId = (Guid?)productId, text = (string?)null, amount };

    private static object Text(string text) => new { productId = (Guid?)null, text = (string?)text, amount = (decimal?)null };

    private static async Task<JsonElement> AddAsync(ApiClient client, Guid fridgeId, params object[] items) =>
        await IsJsonArrayAsync(await client.PostJsonAsync($"/api/v1/fridges/{fridgeId}/shopping-list/items", new { items }), "ShoppingListItem");

    private static async Task<JsonElement> ListAsync(ApiClient client, Guid fridgeId) =>
        await IsJsonArrayAsync(await client.GetAsync($"/api/v1/fridges/{fridgeId}/shopping-list"), "ShoppingListItem");

    private static async Task<JsonElement> PatchAsync(ApiClient client, Guid fridgeId, JsonElement entry, object body) =>
        await ContractAssert.IsJsonAsync(await PatchJsonAsync(client, ItemPath(fridgeId, entry), body), HttpStatusCode.OK, "ShoppingListItem");

    private static string ItemPath(Guid fridgeId, JsonElement entry) => $"/api/v1/fridges/{fridgeId}/shopping-list/items/{entry.GetProperty("id").GetGuid()}";

    private static uint Version(JsonElement entry) => entry.GetProperty("version").GetUInt32();

    private static string Name(JsonElement entry) =>
        entry.GetProperty("product").ValueKind == JsonValueKind.Null
            ? entry.GetProperty("text").GetString()!
            : entry.GetProperty("product").GetProperty("name").GetString()!;
}
