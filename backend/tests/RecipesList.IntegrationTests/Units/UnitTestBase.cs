using System.Net;
using System.Text.Json;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.IntegrationTests.Catalog;
using RecipesList.IntegrationTests.Infrastructure;

namespace RecipesList.IntegrationTests.Units;

/// <summary>Shared arrangement for the custom-unit tests (PROJECT §5.6): products with and without conversion data.</summary>
public abstract class UnitTestBase(ApiFactory factory) : CatalogTestBase(factory)
{
    protected static readonly object CanForms = new { one = "puszka", few = "puszki", many = "puszek", fraction = "puszki" };
    protected static readonly object CupForms = new { one = "filiżanka", few = "filiżanki", many = "filiżanek", fraction = "filiżanki" };

    /// <summary>Adds a product directly to the catalog.</summary>
    protected async Task<Guid> AddFoodAsync(string name, MeasureKind kind, decimal? gramsPerMl = null, decimal? gramsPerPiece = null)
    {
        await using var db = Factory.CreateDbContext();
        var product = new Product
        {
            Name = name,
            NormalizedName = NameNormalizer.Normalize(name),
            Category = ProductCategory.Other,
            MeasureKind = kind,
            DisplayClass = kind switch
            {
                MeasureKind.Mass => DisplayClass.Weight,
                MeasureKind.Volume => DisplayClass.Liquid,
                _ => DisplayClass.Piece,
            },
            GramsPerMl = gramsPerMl,
            GramsPerPiece = gramsPerPiece,
            CreatedAt = TestData.Now,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id;
    }

    /// <summary>A <c>CustomUnitWrite</c> body: "1 <paramref name="name"/> = <paramref name="value"/> <paramref name="unit"/>".</summary>
    protected static object UnitBody(string name, object forms, Guid? productId, decimal value, string unit, string? size = null) => new
    {
        name,
        forms,
        productId,
        equals = new { value, unit, size },
    };

    /// <summary>Creates (or finds) a unit and returns its <c>CustomUnitRef</c>.</summary>
    protected static async Task<JsonElement> CreateUnitAsync(ApiClient client, object body, HttpStatusCode expected = HttpStatusCode.Created) =>
        await ContractAssert.IsJsonAsync(await client.PostJsonAsync("/api/v1/units", body), expected, "CustomUnitRef");

    protected static object Custom(decimal value, Guid unitId) => new { value, unit = "custom", customUnitId = unitId };

    protected static object Builtin(decimal value, string unit) => new { value, unit };

    protected static object Ingredient(Guid productId, object? quantity, Guid? id = null) => new
    {
        id,
        productId,
        quantity,
        note = (string?)null,
        optional = false,
    };

    /// <summary>A recipe body with the given ingredient rows and one instruction step.</summary>
    protected static Dictionary<string, object?> RecipeBody(params object[] ingredients) => new()
    {
        ["title"] = "Sos pomidorowy",
        ["description"] = null,
        ["coverImageId"] = null,
        ["prepMinutes"] = null,
        ["cookMinutes"] = null,
        ["tagIds"] = Array.Empty<Guid>(),
        ["utensilIds"] = Array.Empty<Guid>(),
        ["ingredients"] = ingredients,
        ["prepSteps"] = Array.Empty<object>(),
        ["instructionSteps"] = new object[] { new { id = (Guid?)null, blocks = new object[] { new { type = "text", text = "Gotuj." } } } },
        ["visibility"] = "private",
    };

    protected static async Task<JsonElement> CreateRecipeAsync(ApiClient client, params object[] ingredients) =>
        await ContractAssert.IsJsonAsync(await client.PostJsonAsync("/api/v1/recipes", RecipeBody(ingredients)), HttpStatusCode.Created, "RecipeDetail");

    /// <summary>The <c>VariantWrite.recipe</c> copy of <paramref name="recipe"/>, as the editor sends it back.</summary>
    protected static Dictionary<string, object?> VariantCopy(JsonElement recipe) => new()
    {
        ["prepMinutes"] = recipe.GetProperty("prepMinutes"),
        ["cookMinutes"] = recipe.GetProperty("cookMinutes"),
        ["utensilIds"] = recipe.GetProperty("utensils").EnumerateArray().Select(u => u.GetProperty("id")).ToList(),
        ["ingredients"] = recipe.GetProperty("ingredients").EnumerateArray().Select(i => (object)new Dictionary<string, object?>
        {
            ["id"] = i.GetProperty("id"),
            ["productId"] = i.GetProperty("product").GetProperty("id"),
            ["quantity"] = i.GetProperty("quantity"),
            ["note"] = i.GetProperty("note"),
            ["optional"] = i.GetProperty("optional"),
        }).ToList(),
        ["prepSteps"] = Steps(recipe, "prepSteps"),
        ["instructionSteps"] = Steps(recipe, "instructionSteps"),
    };

    private static List<object> Steps(JsonElement recipe, string name) =>
        [.. recipe.GetProperty(name).EnumerateArray().Select(s => (object)new { id = s.GetProperty("id"), blocks = s.GetProperty("blocks") })];
}
