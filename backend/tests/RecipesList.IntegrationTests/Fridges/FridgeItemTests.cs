using System.Net;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Fridges;

/// <summary>Fridge items (API.md §6, PROJECT §8.2).</summary>
public sealed class FridgeItemTests(ApiFactory factory) : FridgeTestBase(factory)
{
    [Fact]
    public async Task CreateItem_ComputesRemainingAmount()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);

        var item = await AddItemAsync(anna, fridgeId, Cream, 330m, 70m, "  otwarta  ");

        item.GetProperty("product").GetProperty("name").GetString().ShouldBe("Śmietanka 30%");
        item.GetProperty("packageAmount").GetDecimal().ShouldBe(330m);
        item.GetProperty("remainingPercent").GetDecimal().ShouldBe(70m);
        item.GetProperty("remainingAmount").GetDecimal().ShouldBe(231m);
        item.GetProperty("note").GetString().ShouldBe("otwarta");
        item.GetProperty("addedBy").GetProperty("nickname").GetString().ShouldBe("anna");
        var fridge = await ApiClient.ReadJsonAsync(await anna.GetAsync($"/api/v1/fridges/{fridgeId}"));
        fridge.GetProperty("itemCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task CreateItem_InvalidValues_Returns400WithFieldErrors()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);

        var problem = await ContractAssert.IsProblemAsync(
            await anna.PostJsonAsync($"/api/v1/fridges/{fridgeId}/items", new { productId = Guid.CreateVersion7(), packageAmount = 0, remainingPercent = 0, note = (string?)null }),
            HttpStatusCode.BadRequest,
            "validation.failed");

        var errors = problem.GetProperty("errors");
        errors.GetProperty("productId")[0].GetString().ShouldBe("product.not_found");
        errors.GetProperty("packageAmount")[0].GetString().ShouldBe("out_of_range");
        errors.GetProperty("remainingPercent")[0].GetString().ShouldBe("out_of_range");
    }

    [Fact]
    public async Task ListItems_OrdersByCategoryThenNameThenMostUsed()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        await AddItemAsync(anna, fridgeId, Flour, 1000m);
        await AddItemAsync(anna, fridgeId, Milk, 1000m, 100m);
        await AddItemAsync(anna, fridgeId, Milk, 1000m, 30m);
        await AddItemAsync(anna, fridgeId, Eggs, 10m);

        var items = await ItemsAsync(anna, fridgeId);

        items.EnumerateArray()
            .Select(i => (i.GetProperty("product").GetProperty("name").GetString(), i.GetProperty("remainingPercent").GetDecimal()))
            .ShouldBe([("Jajka", 100m), ("Mleko", 30m), ("Mleko", 100m), ("Mąka pszenna", 100m)]);
    }

    [Fact]
    public async Task UpdateItem_CurrentVersion_SavesAndChangesVersion()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var item = await AddItemAsync(anna, fridgeId, Milk, 1000m);
        var id = item.GetProperty("id").GetGuid();
        var version = item.GetProperty("version").GetUInt32();

        var updated = await ContractAssert.IsJsonAsync(
            await anna.PutJsonAsync($"/api/v1/fridges/{fridgeId}/items/{id}", new { productId = Milk, packageAmount = 1000, remainingPercent = 33.333m, note = "na kawę", version }),
            HttpStatusCode.OK,
            "FridgeItem");

        updated.GetProperty("remainingPercent").GetDecimal().ShouldBe(33.33m);
        updated.GetProperty("remainingAmount").GetDecimal().ShouldBe(333.3m);
        updated.GetProperty("note").GetString().ShouldBe("na kawę");
        updated.GetProperty("version").GetUInt32().ShouldNotBe(version);
    }

    [Fact]
    public async Task UpdateItem_StaleVersion_Returns409()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var item = await AddItemAsync(anna, fridgeId, Milk, 1000m);
        var id = item.GetProperty("id").GetGuid();
        var version = item.GetProperty("version").GetUInt32();
        (await anna.PutJsonAsync($"/api/v1/fridges/{fridgeId}/items/{id}", new { productId = Milk, packageAmount = 1000, remainingPercent = 50, note = (string?)null, version }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        await ContractAssert.IsProblemAsync(
            await anna.PutJsonAsync($"/api/v1/fridges/{fridgeId}/items/{id}", new { productId = Milk, packageAmount = 1000, remainingPercent = 40, note = (string?)null, version }),
            HttpStatusCode.Conflict,
            "concurrency.conflict");
    }

    [Fact]
    public async Task UpdateItem_ZeroPercent_DeletesAndReturns204()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var item = await AddItemAsync(anna, fridgeId, Milk, 1000m);
        var id = item.GetProperty("id").GetGuid();

        var response = await anna.PutJsonAsync(
            $"/api/v1/fridges/{fridgeId}/items/{id}",
            new { productId = Milk, packageAmount = 1000, remainingPercent = 0, note = (string?)null, version = item.GetProperty("version").GetUInt32() });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await ItemsAsync(anna, fridgeId)).GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task DeleteItem_MemberDeletes_UnknownReturns404()
    {
        using var anna = await LoginAsync("anna");
        var fridgeId = await DefaultFridgeAsync(anna);
        var item = await AddItemAsync(anna, fridgeId, Milk, 1000m);
        var path = $"/api/v1/fridges/{fridgeId}/items/{item.GetProperty("id").GetGuid()}";

        (await anna.DeleteAsync(path)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await ContractAssert.IsProblemAsync(await anna.DeleteAsync(path), HttpStatusCode.NotFound, "fridge_item.not_found");
    }

    [Fact]
    public async Task Items_ItemOfAnotherFridge_Returns404()
    {
        using var anna = await LoginAsync("anna");
        var first = await DefaultFridgeAsync(anna);
        var second = (await ApiClient.ReadJsonAsync(await anna.PostJsonAsync("/api/v1/fridges", new { name = "Druga" }))).GetProperty("id").GetGuid();
        var item = await AddItemAsync(anna, first, Milk, 1000m);

        await ContractAssert.IsProblemAsync(
            await anna.DeleteAsync($"/api/v1/fridges/{second}/items/{item.GetProperty("id").GetGuid()}"), HttpStatusCode.NotFound, "fridge_item.not_found");
    }
}
