using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Variants;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Recipes;

/// <summary>
/// Servings end to end (PROJECT §5.7, D-37): recipe CRUD, the variant override, export/import both ways and the
/// servings form of the remembered scaling choice (API.md §4, §5, §5.2, §8).
/// </summary>
public sealed class ServingsTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private ApiClient _ania = null!;
    private ApiClient _bartek = null!;
    private Guid _flour;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
        _ania = factory.CreateApiClient();
        _bartek = factory.CreateApiClient();
        await _ania.LoginAsync("ania");
        await _bartek.LoginAsync("bartek");

        await using var db = factory.CreateDbContext();
        var flour = new Product
        {
            Name = "Mąka",
            NormalizedName = NameNormalizer.Normalize("Mąka"),
            MeasureKind = MeasureKind.Mass,
            DisplayClass = DisplayClass.Weight,
            CreatedAt = TestData.Now,
        };
        db.Products.Add(flour);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        _flour = flour.Id;
    }

    public ValueTask DisposeAsync()
    {
        _ania.Dispose();
        _bartek.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Recipe_CreateReadUpdate_KeepsServings()
    {
        // Private: public recipes are read-only (PROJECT §4.5).
        var created = await ContractAssert.IsJsonAsync(await _ania.PostJsonAsync("/api/v1/recipes", Body(4, visibility: "private")), HttpStatusCode.Created, "RecipeDetail");
        created.GetProperty("servings").GetInt32().ShouldBe(4);
        var id = created.GetProperty("id").GetGuid();

        var read = await ContractAssert.IsJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{id}"), HttpStatusCode.OK, "RecipeDetail");
        read.GetProperty("servings").GetInt32().ShouldBe(4);

        var update = Body(6);
        update["version"] = read.GetProperty("version").GetUInt32();
        update["deleteVariants"] = false;
        var updated = await ContractAssert.IsJsonAsync(await _ania.PutJsonAsync($"/api/v1/recipes/{id}", update), HttpStatusCode.OK, "RecipeDetail");
        updated.GetProperty("servings").GetInt32().ShouldBe(6);

        update = Body(null);
        update["version"] = updated.GetProperty("version").GetUInt32();
        update["deleteVariants"] = false;
        var cleared = await ContractAssert.IsJsonAsync(await _ania.PutJsonAsync($"/api/v1/recipes/{id}", update), HttpStatusCode.OK, "RecipeDetail");
        cleared.GetProperty("servings").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Recipe_ServingsOutOfRange_Returns400(int servings)
    {
        var response = await _ania.PostJsonAsync("/api/v1/recipes", Body(servings));

        var problem = await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
        problem.GetProperty("errors").TryGetProperty("servings", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Variant_OverridingServings_ResolvesToTheOverride_InheritingResolvesToTheBase()
    {
        var recipe = await CreateAsync(4);
        var recipeId = recipe.GetProperty("id").GetGuid();

        var overriding = await PostVariantAsync(recipe, "Na imprezę", servings: 10);
        overriding.GetProperty("servingsOverride").GetInt32().ShouldBe(10);
        overriding.GetProperty("changeCount").GetInt32().ShouldBe(1);
        var resolved = await ContractAssert.IsJsonAsync(
            await _ania.GetAsync($"/api/v1/variants/{overriding.GetProperty("id").GetGuid()}/resolved"), HttpStatusCode.OK, "ResolvedRecipe");
        resolved.GetProperty("servings").GetInt32().ShouldBe(10);

        // The same servings as the base is no change at all.
        var same = await _ania.PostJsonAsync($"/api/v1/recipes/{recipeId}/variants", VariantBody(recipe, "Bez zmian", servings: 4));
        await ContractAssert.IsProblemAsync(same, HttpStatusCode.BadRequest, ErrorCodes.VariantNoChanges);

        // A cleared value inherits the base's servings.
        var inheriting = await PostVariantAsync(recipe, "Więcej mąki", servings: null, flour: 300);
        inheriting.GetProperty("servingsOverride").ValueKind.ShouldBe(JsonValueKind.Null);
        var inherited = await ContractAssert.IsJsonAsync(
            await _ania.GetAsync($"/api/v1/variants/{inheriting.GetProperty("id").GetGuid()}/resolved"), HttpStatusCode.OK, "ResolvedRecipe");
        inherited.GetProperty("servings").GetInt32().ShouldBe(4);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Variant_ServingsOutOfRange_Returns400(int servings)
    {
        var recipe = await CreateAsync(4);

        var response = await _ania.PostJsonAsync($"/api/v1/recipes/{recipe.GetProperty("id").GetGuid()}/variants", VariantBody(recipe, "Zła", servings));

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task ExportImport_RoundTrip_KeepsServingsAndTheOverride()
    {
        var recipe = await CreateAsync(4);
        var recipeId = recipe.GetProperty("id").GetGuid();
        await PostVariantAsync(recipe, "Na imprezę", servings: 10);

        var file = await ExportAsync(recipeId);
        file["recipe"]!["servings"]!.GetValue<int>().ShouldBe(4);
        file["variants"]![0]!["servingsOverride"]!.GetValue<int>().ShouldBe(10);

        var imported = await ContractAssert.IsJsonAsync(await _bartek.PostJsonAsync("/api/v1/recipes/import", file), HttpStatusCode.Created, "RecipeDetail");
        imported.GetProperty("servings").GetInt32().ShouldBe(4);
        await using var db = factory.CreateDbContext();
        var importedId = imported.GetProperty("id").GetGuid();
        (await db.Variants.SingleAsync(v => v.RecipeId == importedId, TestContext.Current.CancellationToken)).ServingsOverride.ShouldBe(10);
    }

    [Fact]
    public async Task Import_FileWithoutServings_ImportsAsBefore()
    {
        var recipe = await CreateAsync(4);
        var file = await ExportAsync(recipe.GetProperty("id").GetGuid());
        file["recipe"]!.AsObject().Remove("servings");

        var imported = await ContractAssert.IsJsonAsync(await _bartek.PostJsonAsync("/api/v1/recipes/import", file), HttpStatusCode.Created, "RecipeDetail");

        imported.GetProperty("servings").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task ImportAsVariants_DifferentServings_BecomeAnOverride_MissingKeepsTheTarget()
    {
        var target = await CreateAsync(4);
        var targetId = target.GetProperty("id").GetGuid();
        var source = await CreateAsync(6, flour: 300);
        var file = await ExportAsync(source.GetProperty("id").GetGuid());

        var result = await ContractAssert.IsJsonAsync(
            await _bartek.PostJsonAsync($"/api/v1/recipes/{targetId}/variants/import", file), HttpStatusCode.Created, "VariantImportResult");
        var variantId = result.GetProperty("variants")[0].GetProperty("id").GetGuid();
        var detail = await ContractAssert.IsJsonAsync(await _bartek.GetAsync($"/api/v1/variants/{variantId}"), HttpStatusCode.OK, "VariantDetail");
        detail.GetProperty("servingsOverride").GetInt32().ShouldBe(6);

        // Without servings in the file the target's are kept: only the flour differs.
        var other = await CreateAsync(null, flour: 400);
        var withoutServings = await ExportAsync(other.GetProperty("id").GetGuid());
        var second = await ContractAssert.IsJsonAsync(
            await _ania.PostJsonAsync($"/api/v1/recipes/{targetId}/variants/import", withoutServings), HttpStatusCode.Created, "VariantImportResult");
        var secondId = second.GetProperty("variants")[0].GetProperty("id").GetGuid();
        var resolved = await ContractAssert.IsJsonAsync(await _ania.GetAsync($"/api/v1/variants/{secondId}/resolved"), HttpStatusCode.OK, "ResolvedRecipe");
        resolved.GetProperty("servings").GetInt32().ShouldBe(4);
    }

    [Fact]
    public async Task MySelection_AcceptsBothScalingForms()
    {
        var recipe = await CreateAsync(4);
        var recipeId = recipe.GetProperty("id").GetGuid();
        var slot = $"i:{recipe.GetProperty("ingredients")[0].GetProperty("id").GetGuid()}";

        var servings = await ContractAssert.IsJsonAsync(
            await _ania.PutJsonAsync($"/api/v1/recipes/{recipeId}/my-selection", new { picks = new { }, scaling = new { kind = "servings", servings = 6 } }),
            HttpStatusCode.OK,
            "Selection");
        servings.GetProperty("scaling").GetProperty("kind").GetString().ShouldBe("servings");
        servings.GetProperty("scaling").GetProperty("servings").GetInt32().ShouldBe(6);
        var remembered = await ContractAssert.IsJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipeId}/my-selection"), HttpStatusCode.OK, "Selection");
        remembered.GetProperty("scaling").GetProperty("servings").GetInt32().ShouldBe(6);

        // Without a kind the anchor form is meant (selections stored before servings existed).
        var anchor = await ContractAssert.IsJsonAsync(
            await _ania.PutJsonAsync($"/api/v1/recipes/{recipeId}/my-selection", new { picks = new { }, scaling = new { anchorSlotKey = slot, anchorBaseAmount = 150 } }),
            HttpStatusCode.OK,
            "Selection");
        anchor.GetProperty("scaling").GetProperty("kind").GetString().ShouldBe("ingredient");
        anchor.GetProperty("scaling").GetProperty("anchorBaseAmount").GetDecimal().ShouldBe(150m);

        var explicitAnchor = await _ania.PutJsonAsync(
            $"/api/v1/recipes/{recipeId}/my-selection", new { picks = new { }, scaling = new { kind = "ingredient", anchorSlotKey = slot, anchorBaseAmount = 100 } });
        await ContractAssert.IsJsonAsync(explicitAnchor, HttpStatusCode.OK, "Selection");
    }

    public static TheoryData<string> InvalidScalings => new()
    {
        """{ "kind": "servings", "servings": 0 }""",
        """{ "kind": "servings", "servings": 101 }""",
        """{ "kind": "servings" }""",
        """{ "kind": "servings", "servings": 4, "anchorSlotKey": "i:00000000-0000-0000-0000-000000000000", "anchorBaseAmount": 1 }""",
        """{ "anchorSlotKey": "i:00000000-0000-0000-0000-000000000000", "anchorBaseAmount": 0 }""",
        """{ "anchorBaseAmount": 5 }""",
    };

    [Theory]
    [MemberData(nameof(InvalidScalings))]
    public async Task MySelection_InvalidScaling_Returns400(string scaling)
    {
        var recipe = await CreateAsync(4);
        var body = JsonNode.Parse($$"""{ "picks": {}, "scaling": {{scaling}} }""");

        var response = await _ania.PutJsonAsync($"/api/v1/recipes/{recipe.GetProperty("id").GetGuid()}/my-selection", body);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, ErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task MySelection_UnknownScalingKind_IsAnInvalidRequest()
    {
        var recipe = await CreateAsync(4);
        var body = JsonNode.Parse("""{ "picks": {}, "scaling": { "kind": "people", "servings": 4 } }""");

        var response = await _ania.PutJsonAsync($"/api/v1/recipes/{recipe.GetProperty("id").GetGuid()}/my-selection", body);

        await ContractAssert.IsProblemAsync(response, HttpStatusCode.BadRequest, "request.invalid");
    }

    [Fact]
    public async Task StoredSelectionWithoutKind_ReadsAsTheIngredientForm()
    {
        var recipe = await CreateAsync(4);
        var recipeId = recipe.GetProperty("id").GetGuid();
        var slot = $"i:{recipe.GetProperty("ingredients")[0].GetProperty("id").GetGuid()}";
        await using (var db = factory.CreateDbContext())
        {
            var json = $$"""{"anchorSlotKey":"{{slot}}","anchorBaseAmount":50}""";
            await db.Database.ExecuteSqlAsync(
                $"INSERT INTO recipe_selections (user_id, recipe_id, picks, scaling, updated_at) VALUES ({_ania.UserId}, {recipeId}, '{{}}'::jsonb, {json}::jsonb, now())",
                TestContext.Current.CancellationToken);
        }

        var remembered = await ContractAssert.IsJsonAsync(await _ania.GetAsync($"/api/v1/recipes/{recipeId}/my-selection"), HttpStatusCode.OK, "Selection");

        remembered.GetProperty("scaling").GetProperty("kind").GetString().ShouldBe("ingredient");
        remembered.GetProperty("scaling").GetProperty("anchorSlotKey").GetString().ShouldBe(slot);
    }

    [Fact]
    public async Task DeletingAVariant_KeepsAServingsChoice()
    {
        var recipe = await CreateAsync(4);
        var recipeId = recipe.GetProperty("id").GetGuid();
        var variant = await PostVariantAsync(recipe, "Na imprezę", servings: 10);
        await _ania.PutJsonAsync($"/api/v1/recipes/{recipeId}/my-selection", new { picks = new { }, scaling = new { kind = "servings", servings = 3 } });

        (await _ania.DeleteAsync($"/api/v1/variants/{variant.GetProperty("id").GetGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await using var db = factory.CreateDbContext();
        var selection = await db.RecipeSelections.AsNoTracking().SingleAsync(s => s.RecipeId == recipeId, TestContext.Current.CancellationToken);
        selection.Scaling.ShouldBe(ScalingState.ForServings(3));
    }

    private Dictionary<string, object?> Body(int? servings, decimal flour = 200, string visibility = "public") => new()
    {
        ["title"] = "Naleśniki",
        ["description"] = null,
        ["coverImageId"] = null,
        ["prepMinutes"] = 10,
        ["cookMinutes"] = 20,
        ["servings"] = servings,
        ["tagIds"] = Array.Empty<Guid>(),
        ["utensilIds"] = Array.Empty<Guid>(),
        ["ingredients"] = new object[] { new { id = (Guid?)null, productId = _flour, quantity = new { value = flour, unit = "g" }, note = (string?)null, optional = false } },
        ["prepSteps"] = Array.Empty<object>(),
        ["instructionSteps"] = new object[] { new { id = (Guid?)null, blocks = new object[] { new { type = "text", text = "Smaż." } } } },
        ["visibility"] = visibility,
    };

    private async Task<JsonElement> CreateAsync(int? servings, decimal flour = 200)
    {
        var response = await _ania.PostJsonAsync("/api/v1/recipes", Body(servings, flour));
        return await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "RecipeDetail");
    }

    private static object VariantBody(JsonElement recipe, string name, int? servings, decimal? flour = null)
    {
        var row = recipe.GetProperty("ingredients")[0];
        return new
        {
            name,
            description = (string?)null,
            visibility = "public",
            recipe = new
            {
                prepMinutes = recipe.GetProperty("prepMinutes").GetInt32(),
                cookMinutes = recipe.GetProperty("cookMinutes").GetInt32(),
                servings,
                utensilIds = Array.Empty<Guid>(),
                ingredients = new object[]
                {
                    new
                    {
                        id = row.GetProperty("id").GetGuid(),
                        productId = row.GetProperty("product").GetProperty("id").GetGuid(),
                        quantity = new { value = flour ?? row.GetProperty("quantity").GetProperty("value").GetDecimal(), unit = "g" },
                        note = (string?)null,
                        optional = false,
                    },
                },
                prepSteps = Array.Empty<object>(),
                instructionSteps = recipe.GetProperty("instructionSteps").EnumerateArray()
                    .Select(s => (object)new { id = s.GetProperty("id").GetGuid(), blocks = s.GetProperty("blocks") })
                    .ToArray(),
            },
        };
    }

    private async Task<JsonElement> PostVariantAsync(JsonElement recipe, string name, int? servings, decimal? flour = null)
    {
        var response = await _ania.PostJsonAsync($"/api/v1/recipes/{recipe.GetProperty("id").GetGuid()}/variants", VariantBody(recipe, name, servings, flour));
        return await ContractAssert.IsJsonAsync(response, HttpStatusCode.Created, "VariantDetail");
    }

    private async Task<JsonNode> ExportAsync(Guid recipeId)
    {
        var response = await _ania.GetAsync($"/api/v1/recipes/{recipeId}/export");
        var body = await ContractAssert.IsJsonAsync(response, HttpStatusCode.OK, "RecipeExport");
        return JsonNode.Parse(body.GetRawText())!;
    }
}
