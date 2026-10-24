using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Variants;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Units;

/// <summary>GET/POST /units, PUT /admin/units/{id} and units in a product merge (API.md §2.1, PROJECT §5.6).</summary>
public sealed class CustomUnitEndpointsTests(ApiFactory factory) : UnitTestBase(factory)
{
    [Fact]
    public async Task Create_ProductUnit_StoresTheBaseAmountOfTheProduct()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");

        var response = await ania.PostJsonAsync("/api/v1/units", UnitBody(" Puszka ", CanForms, tomatoes, 0.4m, "kg"));

        var unit = await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "CustomUnitRef");
        response.Headers.Location.ShouldNotBeNull();
        unit.GetProperty("name").GetString().ShouldBe("Puszka");
        unit.GetProperty("productId").GetGuid().ShouldBe(tomatoes);
        unit.GetProperty("measureKind").GetString().ShouldBe("mass");
        unit.GetProperty("amount").GetDecimal().ShouldBe(400m);
        unit.GetProperty("forms").GetProperty("many").GetString().ShouldBe("puszek");
    }

    [Fact]
    public async Task Create_ProductUnitInAnotherKind_ConvertsWithTheDensity()
    {
        var butter = await AddFoodAsync("Masło", MeasureKind.Mass, gramsPerMl: 0.9m);
        using var ania = await LoginAsync("ania");

        var unit = await CreateUnitAsync(ania, UnitBody("łyżka stołowa", CanForms, butter, 1, "glass"));

        unit.GetProperty("measureKind").GetString().ShouldBe("mass");
        unit.GetProperty("amount").GetDecimal().ShouldBe(225m);
    }

    [Fact]
    public async Task Create_UnitForEveryProduct_StoresTheNativeAmountWithoutSizes()
    {
        using var ania = await LoginAsync("ania");

        var cup = await CreateUnitAsync(ania, UnitBody("filiżanka", CupForms, null, 0.6m, "glass"));
        var bunch = await CreateUnitAsync(ania, UnitBody("pęczek", CanForms, null, 2, "piece", "large"));

        cup.GetProperty("productId").ValueKind.ShouldBe(JsonValueKind.Null);
        cup.GetProperty("measureKind").GetString().ShouldBe("volume");
        cup.GetProperty("amount").GetDecimal().ShouldBe(150m);
        bunch.GetProperty("measureKind").GetString().ShouldBe("count");
        bunch.GetProperty("amount").GetDecimal().ShouldBe(2m);
    }

    [Fact]
    public async Task Create_SameNormalizedNameInTheSameScope_ReturnsTheExistingUnit()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        using var bartek = await LoginAsync("bartek");
        var first = await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"));

        var again = await CreateUnitAsync(bartek, UnitBody("PUSZKA", CanForms, tomatoes, 500, "g"), HttpStatusCode.OK);
        var global = await CreateUnitAsync(bartek, UnitBody("puszka", CanForms, null, 400, "g"));

        again.GetProperty("id").GetGuid().ShouldBe(first.GetProperty("id").GetGuid());
        again.GetProperty("amount").GetDecimal().ShouldBe(400m);
        global.GetProperty("id").GetGuid().ShouldNotBe(first.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Create_ForAnUnknownProduct_Returns404()
    {
        using var ania = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(
            await ania.PostJsonAsync("/api/v1/units", UnitBody("puszka", CanForms, Guid.CreateVersion7(), 400, "g")),
            HttpStatusCode.NotFound,
            "product.not_found");
    }

    [Fact]
    public async Task Create_ProductUnitThatCannotBeConverted_Returns400UnitInvalid()
    {
        var flour = await AddFoodAsync("Mąka", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");

        await ContractAssert.IsProblemAsync(
            await ania.PostJsonAsync("/api/v1/units", UnitBody("kubek", CupForms, flour, 1, "glass")),
            HttpStatusCode.BadRequest,
            "unit.invalid");
    }

    [Fact]
    public async Task Create_WithBadShape_Returns400ValidationFailed()
    {
        using var ania = await LoginAsync("ania");

        var problem = await ContractAssert.IsProblemAsync(
            await ania.PostJsonAsync("/api/v1/units", new
            {
                name = " ",
                forms = new { one = "a", few = "", many = new string('x', 41), fraction = "d" },
                productId = (Guid?)null,
                equals = new { value = 1, unit = "custom", size = (string?)null },
            }),
            HttpStatusCode.BadRequest,
            "validation.failed");

        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("name", out _).ShouldBeTrue();
        errors.TryGetProperty("forms.few", out _).ShouldBeTrue();
        errors.TryGetProperty("forms.many", out _).ShouldBeTrue();
        errors.TryGetProperty("equals.unit", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Search_ListsProductUnitsFirstThenUnitsForEveryProductByName()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        var butter = await AddFoodAsync("Masło", MeasureKind.Mass);
        using var ania = await LoginAsync("ania");
        await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"));
        await CreateUnitAsync(ania, UnitBody("kostka", CanForms, butter, 200, "g"));
        await CreateUnitAsync(ania, UnitBody("garść", CanForms, null, 30, "g"));
        await CreateUnitAsync(ania, UnitBody("filiżanka", CupForms, null, 150, "ml"));

        var forTomatoesResponse = await ania.GetAsync($"/api/v1/units?productId={tomatoes}");
        forTomatoesResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forTomatoes = await ApiClient.ReadJsonAsync(forTomatoesResponse);
        foreach (var unit in forTomatoes.EnumerateArray())
        {
            ContractAssert.MatchesSchema(unit, "CustomUnitRef");
        }

        var everyProduct = await ania.GetAsync("/api/v1/units");
        var filtered = await ania.GetAsync($"/api/v1/units?productId={tomatoes}&q=FILI");

        NamesOf(forTomatoes).ShouldBe(["puszka", "filiżanka", "garść"]);
        NamesOf(await ApiClient.ReadJsonAsync(everyProduct)).ShouldBe(["filiżanka", "garść"]);
        NamesOf(await ApiClient.ReadJsonAsync(filtered)).ShouldBe(["filiżanka"]);
    }

    [Fact]
    public async Task AdminUpdate_AsNormalUser_Returns403()
    {
        using var ania = await LoginAsync("ania");
        var unit = await CreateUnitAsync(ania, UnitBody("garść", CanForms, null, 30, "g"));

        await ContractAssert.IsProblemAsync(
            await ania.PutJsonAsync($"/api/v1/admin/units/{unit.GetProperty("id").GetGuid()}", UnitBody("garść", CanForms, null, 40, "g")),
            HttpStatusCode.Forbidden,
            "admin.required");
    }

    [Fact]
    public async Task AdminUpdate_UnknownUnit_Returns404()
    {
        using var admin = await LoginAdminAsync();

        await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/units/{Guid.CreateVersion7()}", UnitBody("garść", CanForms, null, 40, "g")),
            HttpStatusCode.NotFound,
            "unit.not_found");
    }

    [Fact]
    public async Task AdminUpdate_UnusedUnit_ChangesNameFormsAndAmount()
    {
        using var admin = await LoginAdminAsync();
        var unit = await CreateUnitAsync(admin, UnitBody("garść", CanForms, null, 30, "g"));

        var updated = await ContractAssert.IsJsonAsync(
            await admin.PutJsonAsync($"/api/v1/admin/units/{unit.GetProperty("id").GetGuid()}", UnitBody("Garstka", CupForms, null, 25, "ml")),
            HttpStatusCode.OK,
            "CustomUnitRef");

        updated.GetProperty("name").GetString().ShouldBe("Garstka");
        updated.GetProperty("forms").GetProperty("one").GetString().ShouldBe("filiżanka");
        updated.GetProperty("measureKind").GetString().ShouldBe("volume");
        updated.GetProperty("amount").GetDecimal().ShouldBe(25m);
    }

    [Fact]
    public async Task AdminUpdate_UsedByARecipe_RenamesButRefusesANewAmount()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var admin = await LoginAdminAsync();
        using var ania = await LoginAsync("ania");
        var unit = await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"));
        var unitId = unit.GetProperty("id").GetGuid();
        await CreateRecipeAsync(ania, Ingredient(tomatoes, Custom(2, unitId)));

        var renamed = await ContractAssert.IsJsonAsync(
            await admin.PutJsonAsync($"/api/v1/admin/units/{unitId}", UnitBody("puszka duża", CanForms, tomatoes, 0.4m, "kg")),
            HttpStatusCode.OK,
            "CustomUnitRef");
        await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/units/{unitId}", UnitBody("puszka duża", CanForms, tomatoes, 500, "g")),
            HttpStatusCode.BadRequest,
            "unit.in_use");

        renamed.GetProperty("name").GetString().ShouldBe("puszka duża");
        renamed.GetProperty("amount").GetDecimal().ShouldBe(400m);
    }

    [Fact]
    public async Task AdminUpdate_UsedOnlyByAVariantOp_RefusesANewAmount()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var admin = await LoginAdminAsync();
        using var ania = await LoginAsync("ania");
        var unitId = (await CreateUnitAsync(ania, UnitBody("puszka", CanForms, tomatoes, 400, "g"))).GetProperty("id").GetGuid();
        var recipe = await CreateRecipeAsync(ania, Ingredient(tomatoes, Builtin(800, "g")));
        await using (var db = Factory.CreateDbContext())
        {
            db.Variants.Add(new Variant
            {
                RecipeId = recipe.GetProperty("id").GetGuid(),
                AuthorId = ania.UserId,
                Name = "W puszkach",
                IngredientOps = [new ModifyIngredientOp(recipe.GetProperty("ingredients")[0].GetProperty("id").GetGuid(), tomatoes, new(2, Core.Quantities.Unit.Custom, null, unitId), 800, null, false)],
                CreatedAt = TestData.Now,
                UpdatedAt = TestData.Now,
            });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/units/{unitId}", UnitBody("puszka", CanForms, tomatoes, 500, "g")),
            HttpStatusCode.BadRequest,
            "unit.in_use");
    }

    [Fact]
    public async Task AdminUpdate_ChangingTheScope_Returns400()
    {
        var tomatoes = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var admin = await LoginAdminAsync();
        var unit = await CreateUnitAsync(admin, UnitBody("puszka", CanForms, tomatoes, 400, "g"));

        var problem = await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/units/{unit.GetProperty("id").GetGuid()}", UnitBody("puszka", CanForms, null, 400, "g")),
            HttpStatusCode.BadRequest,
            "validation.failed");

        problem.GetProperty("errors").TryGetProperty("productId", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task AdminUpdate_ToANameTakenInTheScope_Returns400()
    {
        using var admin = await LoginAdminAsync();
        await CreateUnitAsync(admin, UnitBody("garść", CanForms, null, 30, "g"));
        var other = await CreateUnitAsync(admin, UnitBody("szczypior", CanForms, null, 10, "g"));

        var problem = await ContractAssert.IsProblemAsync(
            await admin.PutJsonAsync($"/api/v1/admin/units/{other.GetProperty("id").GetGuid()}", UnitBody("Garść", CanForms, null, 10, "g")),
            HttpStatusCode.BadRequest,
            "validation.failed");

        problem.GetProperty("errors").TryGetProperty("name", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task ProductMerge_MovesUnitsToTheTargetAndRenamesClashes()
    {
        var source = await AddFoodAsync("Pomidory w puszce", MeasureKind.Mass);
        var target = await AddFoodAsync("Pomidory krojone", MeasureKind.Mass);
        using var admin = await LoginAdminAsync();
        await CreateUnitAsync(admin, UnitBody("puszka", CanForms, source, 400, "g"));
        await CreateUnitAsync(admin, UnitBody("słoik", CanForms, source, 300, "g"));
        var kept = await CreateUnitAsync(admin, UnitBody("puszka", CanForms, target, 425, "g"));

        var response = await admin.PostJsonAsync($"/api/v1/admin/products/{source}/merge", new { targetId = target });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var db = Factory.CreateDbContext();
        var units = await db.CustomUnits.AsNoTracking().OrderBy(u => u.NormalizedName).ToListAsync(TestContext.Current.CancellationToken);
        units.Select(u => (u.Name, u.ProductId)).ShouldBe(new (string, Guid?)[] { ("puszka", target), ("puszka (2)", target), ("słoik", target) });
        units.Single(u => u.Name == "puszka").Id.ShouldBe(kept.GetProperty("id").GetGuid());
    }

    private static string[] NamesOf(JsonElement array) => [.. array.EnumerateArray().Select(e => e.GetProperty("name").GetString()!)];
}
