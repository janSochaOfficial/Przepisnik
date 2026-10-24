using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Units;

/// <summary>Custom units in recipes, variants, the matrix and the export file (PROJECT §5.6, API.md §0, §4, §5, §8).</summary>
public sealed class CustomUnitUsageTests(ApiFactory factory) : UnitTestBase(factory)
{
    [Fact]
    public async Task Recipe_WithTwoCans_StoresTheBaseAmountAndEmbedsTheUnit()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var can = await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"));
        var canId = can.GetProperty("id").GetGuid();

        var created = await CreateRecipeAsync(ania, Ingredient(tomatoes, Custom(2, canId)));
        var detail = await ContractAssert.IsJsonAsync(await ania.GetAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}"), HttpStatusCode.OK, "RecipeDetail");

        var row = detail.GetProperty("ingredients")[0];
        row.GetProperty("baseAmount").GetDecimal().ShouldBe(800m);
        var quantity = row.GetProperty("quantity");
        quantity.GetProperty("unit").GetString().ShouldBe("custom");
        quantity.GetProperty("customUnitId").GetGuid().ShouldBe(canId);
        quantity.GetProperty("customUnit").GetProperty("name").GetString().ShouldBe("puszka");
        quantity.GetProperty("customUnit").GetProperty("amount").GetDecimal().ShouldBe(400m);
    }

    [Fact]
    public async Task Recipe_Update_AcceptsTheQuantityAsReturnedWithTheEmbeddedUnit()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var created = await CreateRecipeAsync(ania, Ingredient(tomatoes, Custom(2, canId)));
        var body = RecipeBody(created.GetProperty("ingredients").EnumerateArray().Select(i => (object)new
        {
            id = i.GetProperty("id"),
            productId = i.GetProperty("product").GetProperty("id"),
            quantity = i.GetProperty("quantity"),
            note = i.GetProperty("note"),
            optional = false,
        }).ToArray());
        body["version"] = created.GetProperty("version").GetUInt32();
        body["deleteVariants"] = false;

        var updated = await ContractAssert.IsJsonAsync(
            await ania.PutJsonAsync($"/api/v1/recipes/{created.GetProperty("id").GetGuid()}", body), HttpStatusCode.OK, "RecipeDetail");

        updated.GetProperty("ingredients")[0].GetProperty("quantity").GetProperty("customUnitId").GetGuid().ShouldBe(canId);
    }

    [Fact]
    public async Task Recipe_UnitForEveryProduct_WorksForVolumeAndForMassWithADensity()
    {
        var milk = await AddFoodAsync("Mleko", MeasureKind.Volume, gramsPerMl: 1.03m);
        var flour = await AddFoodAsync("Mąka", MeasureKind.Mass, gramsPerMl: 0.53m);
        using var ania = await LoginAsync("ania");
        var cupId = (await CreateUnitAsync(ania, UnitBody("filiżanka", CupForms, null, 150, "ml"))).GetProperty("id").GetGuid();

        var recipe = await CreateRecipeAsync(ania, Ingredient(milk, Custom(1, cupId)), Ingredient(flour, Custom(2, cupId)));

        recipe.GetProperty("ingredients")[0].GetProperty("baseAmount").GetDecimal().ShouldBe(150m);
        recipe.GetProperty("ingredients")[1].GetProperty("baseAmount").GetDecimal().ShouldBe(159m);
    }

    [Fact]
    public async Task Recipe_UnitThatDoesNotConvert_Returns400UnitIncompatible()
    {
        var flour = await AddFoodAsync("Mąka", MeasureKind.Mass);
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var cupId = (await CreateUnitAsync(ania, UnitBody("filiżanka", CupForms, null, 150, "ml"))).GetProperty("id").GetGuid();
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();

        var noDensity = await ania.PostJsonAsync("/api/v1/recipes", RecipeBody(Ingredient(flour, Custom(1, cupId))));
        var otherProduct = await ania.PostJsonAsync("/api/v1/recipes", RecipeBody(Ingredient(flour, Custom(1, canId))));

        (await ContractAssert.IsProblemAsync(noDensity, HttpStatusCode.BadRequest, "quantity.unit_incompatible"))
            .GetProperty("errors").TryGetProperty("ingredients[0].quantity", out _).ShouldBeTrue();
        await ContractAssert.IsProblemAsync(otherProduct, HttpStatusCode.BadRequest, "quantity.unit_incompatible");
    }

    [Fact]
    public async Task Recipe_UnknownUnitOrMissingId_Returns400()
    {
        var flour = await AddFoodAsync("Mąka", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");

        var unknown = await ania.PostJsonAsync("/api/v1/recipes", RecipeBody(Ingredient(flour, Custom(1, Guid.CreateVersion7()))));
        var missingId = await ania.PostJsonAsync("/api/v1/recipes", RecipeBody(Ingredient(flour, new { value = 1, unit = "custom" })));
        var idWithBuiltin = await ania.PostJsonAsync("/api/v1/recipes", RecipeBody(Ingredient(flour, new { value = 1, unit = "g", customUnitId = Guid.CreateVersion7() })));

        var problem = await ContractAssert.IsProblemAsync(unknown, HttpStatusCode.BadRequest, "validation.failed");
        problem.GetProperty("errors").GetProperty("ingredients[0].quantity")[0].GetString().ShouldBe("unit.not_found");
        await ContractAssert.IsProblemAsync(missingId, HttpStatusCode.BadRequest, "quantity.invalid");
        await ContractAssert.IsProblemAsync(idWithBuiltin, HttpStatusCode.BadRequest, "quantity.invalid");
    }

    [Fact]
    public async Task Variant_ChangingAnIngredientToACustomUnit_ShowsItInDetailResolvedAndMatrix()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var recipe = await CreateRecipeAsync(ania, Ingredient(tomatoes, Builtin(400, "g")));
        var recipeId = recipe.GetProperty("id").GetGuid();
        var copy = VariantCopy(recipe);
        var rows = (List<object>)copy["ingredients"]!;
        ((Dictionary<string, object?>)rows[0])["quantity"] = Custom(2, canId);

        var created = await ContractAssert.IsJsonAsync(
            await ania.PostJsonAsync($"/api/v1/recipes/{recipeId}/variants", new { name = "Dwie puszki", description = (string?)null, visibility = "private", recipe = copy }),
            HttpStatusCode.Created,
            "VariantDetail");
        var variantId = created.GetProperty("id").GetGuid();
        var resolved = await ContractAssert.IsJsonAsync(await ania.GetAsync($"/api/v1/variants/{variantId}/resolved"), HttpStatusCode.OK, "ResolvedRecipe");
        var matrix = await ContractAssert.IsJsonAsync(await ania.GetAsync($"/api/v1/recipes/{recipeId}/variant-matrix"), HttpStatusCode.OK, "VariantMatrix");

        var op = created.GetProperty("ingredientOps")[0];
        op.GetProperty("quantity").GetProperty("customUnit").GetProperty("name").GetString().ShouldBe("puszka");
        var resolvedRow = resolved.GetProperty("ingredients")[0];
        resolvedRow.GetProperty("baseAmount").GetDecimal().ShouldBe(800m);
        resolvedRow.GetProperty("quantity").GetProperty("customUnit").GetProperty("id").GetGuid().ShouldBe(canId);
        var options = matrix.GetProperty("slots")[0].GetProperty("options").EnumerateArray().ToList();
        var canOption = options.Single(o => o.GetProperty("variantIds").EnumerateArray().Any(v => v.GetString() == variantId.ToString()));
        canOption.GetProperty("ingredient").GetProperty("quantity").GetProperty("customUnit").GetProperty("name").GetString().ShouldBe("puszka");
        var baseOption = options.Single(o => o.GetProperty("variantIds").EnumerateArray().Any(v => v.GetString() == "base"));
        baseOption.GetProperty("ingredient").GetProperty("quantity").GetProperty("customUnit").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Variant_ProductUnitOfAnotherProduct_Returns400UnitIncompatible()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        var flour = await AddFoodAsync("Mąka", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var recipe = await CreateRecipeAsync(ania, Ingredient(flour, Builtin(400, "g")));
        var copy = VariantCopy(recipe);
        ((Dictionary<string, object?>)((List<object>)copy["ingredients"]!)[0])["quantity"] = Custom(1, canId);

        await ContractAssert.IsProblemAsync(
            await ania.PostJsonAsync($"/api/v1/recipes/{recipe.GetProperty("id").GetGuid()}/variants", new { name = "Zła", description = (string?)null, visibility = "private", recipe = copy }),
            HttpStatusCode.BadRequest,
            "quantity.unit_incompatible");
    }

    [Fact]
    public async Task Export_ListsTheUnitsAndKeysTheQuantities()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        var milk = await AddFoodAsync("Mleko", MeasureKind.Volume);
        using var ania = await LoginAsync("ania");
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var cupId = (await CreateUnitAsync(ania, UnitBody("filiżanka", CupForms, null, 150, "ml"))).GetProperty("id").GetGuid();
        var recipe = await CreateRecipeAsync(ania, Ingredient(tomatoes, Custom(2, canId)), Ingredient(milk, Custom(1, cupId)), Ingredient(milk, Builtin(100, "ml")));

        var export = await ContractAssert.IsJsonAsync(
            await ania.GetAsync($"/api/v1/recipes/{recipe.GetProperty("id").GetGuid()}/export"), HttpStatusCode.OK, "RecipeExport");

        var units = export.GetProperty("customUnits").EnumerateArray().ToList();
        units.Select(u => u.GetProperty("key").GetString()).ShouldBe(["u1", "u2"]);
        units[0].GetProperty("productKey").GetString().ShouldBe("p1");
        units[0].GetProperty("amount").GetDecimal().ShouldBe(400m);
        units[1].GetProperty("productKey").ValueKind.ShouldBe(JsonValueKind.Null);
        var ingredients = export.GetProperty("recipe").GetProperty("ingredients");
        ingredients[0].GetProperty("quantity").GetProperty("customUnitKey").GetString().ShouldBe("u1");
        ingredients[1].GetProperty("quantity").GetProperty("customUnitKey").GetString().ShouldBe("u2");
        ingredients[2].GetProperty("quantity").TryGetProperty("customUnitKey", out _).ShouldBeFalse();
        export.GetRawText().ShouldNotContain(canId.ToString());
    }

    [Fact]
    public async Task Export_WithoutCustomUnits_HasNoCustomUnitsProperty()
    {
        var flour = await AddFoodAsync("Mąka", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var recipe = await CreateRecipeAsync(ania, Ingredient(flour, Builtin(100, "g")));

        var export = await ContractAssert.IsJsonAsync(
            await ania.GetAsync($"/api/v1/recipes/{recipe.GetProperty("id").GetGuid()}/export"), HttpStatusCode.OK, "RecipeExport");

        export.TryGetProperty("customUnits", out _).ShouldBeFalse();
    }

    [Fact]
    public async Task RoundTrip_WithCustomUnitsInRecipeAndVariant_ReusesTheExistingUnits()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        var milk = await AddFoodAsync("Mleko", MeasureKind.Volume);
        using var ania = await LoginAsync("ania");
        using var bartek = await LoginAsync("bartek");
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var cupId = (await CreateUnitAsync(ania, UnitBody("filiżanka", CupForms, null, 150, "ml"))).GetProperty("id").GetGuid();
        var recipe = await CreateRecipeAsync(ania, Ingredient(tomatoes, Custom(2, canId)), Ingredient(milk, Builtin(100, "ml")));
        var recipeId = recipe.GetProperty("id").GetGuid();
        var copy = VariantCopy(recipe);
        ((Dictionary<string, object?>)((List<object>)copy["ingredients"]!)[1])["quantity"] = Custom(1, cupId);
        (await ania.PostJsonAsync($"/api/v1/recipes/{recipeId}/variants", new { name = "Z filiżanką", description = (string?)null, visibility = "private", recipe = copy }))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        await ania.PutJsonAsync($"/api/v1/recipes/{recipeId}/visibility", new { visibility = "public" });

        var first = await ExportAsync(ania, recipeId);
        var imported = await ContractAssert.IsJsonAsync(await bartek.PostJsonAsync("/api/v1/recipes/import", first), HttpStatusCode.Created, "RecipeDetail");
        var second = await ExportAsync(bartek, imported.GetProperty("id").GetGuid());

        Normalize(second).ShouldBe(Normalize(first));
        imported.GetProperty("ingredients")[0].GetProperty("quantity").GetProperty("customUnitId").GetGuid().ShouldBe(canId);
        await using var db = Factory.CreateDbContext();
        (await db.CustomUnits.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(2);
    }

    [Fact]
    public async Task Import_UnitWithTheSameNameButAnotherAmount_CreatesARenamedUnit()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var recipe = await CreateRecipeAsync(ania, Ingredient(tomatoes, Custom(2, canId)));
        var file = await ExportAsync(ania, recipe.GetProperty("id").GetGuid());
        file["customUnits"]![0]!["amount"] = 425;

        var imported = await ContractAssert.IsJsonAsync(await ania.PostJsonAsync("/api/v1/recipes/import", file), HttpStatusCode.Created, "RecipeDetail");

        var unit = imported.GetProperty("ingredients")[0].GetProperty("quantity").GetProperty("customUnit");
        unit.GetProperty("name").GetString().ShouldBe("puszka (2)");
        unit.GetProperty("amount").GetDecimal().ShouldBe(425m);
        imported.GetProperty("ingredients")[0].GetProperty("baseAmount").GetDecimal().ShouldBe(850m);
    }

    [Fact]
    public async Task Import_ForAProductThatIsCreated_CreatesTheUnitInItsScope()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var recipe = await CreateRecipeAsync(ania, Ingredient(tomatoes, Custom(2, canId)));
        var file = await ExportAsync(ania, recipe.GetProperty("id").GetGuid());
        file["products"]![0]!["name"] = "Pomidory z puszki";

        var imported = await ContractAssert.IsJsonAsync(await ania.PostJsonAsync("/api/v1/recipes/import", file), HttpStatusCode.Created, "RecipeDetail");

        var row = imported.GetProperty("ingredients")[0];
        row.GetProperty("product").GetProperty("name").GetString().ShouldBe("Pomidory z puszki");
        row.GetProperty("quantity").GetProperty("customUnit").GetProperty("productId").GetGuid().ShouldBe(row.GetProperty("product").GetProperty("id").GetGuid());
        row.GetProperty("quantity").GetProperty("customUnit").GetProperty("name").GetString().ShouldBe("puszka");
    }

    [Fact]
    public async Task Import_QuantityWithAnUnknownUnitKey_Returns400InvalidFormat()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        var canId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var recipe = await CreateRecipeAsync(ania, Ingredient(tomatoes, Custom(2, canId)));
        var file = await ExportAsync(ania, recipe.GetProperty("id").GetGuid());
        file["recipe"]!["ingredients"]![0]!["quantity"]!["customUnitKey"] = "u9";

        var problem = await ContractAssert.IsProblemAsync(
            await ania.PostJsonAsync("/api/v1/recipes/import", file), HttpStatusCode.BadRequest, "import.invalid_format");

        problem.GetProperty("errors").TryGetProperty("recipe.ingredients[0].quantity.customUnitKey", out _).ShouldBeTrue();
    }

    private static async Task<JsonNode> ExportAsync(ApiClient client, Guid recipeId)
    {
        var response = await client.GetAsync($"/api/v1/recipes/{recipeId}/export");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!;
    }

    private static string Normalize(JsonNode export)
    {
        var copy = JsonNode.Parse(export.ToJsonString())!;
        copy.AsObject().Remove("exportedAt");
        return copy.ToJsonString();
    }
}
