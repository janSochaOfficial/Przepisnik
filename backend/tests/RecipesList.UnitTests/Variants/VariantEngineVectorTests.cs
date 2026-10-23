using System.Text.Json;
using System.Text.Json.Nodes;
using RecipesList.Core.Variants;

namespace RecipesList.UnitTests.Variants;

/// <summary>Driven by every file in <c>shared/test-vectors/variants/</c> (the frontend matching tests reuse the matrices).</summary>
public sealed class VariantEngineVectorTests
{
    private static readonly IReadOnlyDictionary<string, JsonElement> Cases = TestVectors.Load("variants");
    private readonly VariantEngine _engine = new();

    public static TheoryData<string> CaseIds() => [.. Cases.Keys.Order(StringComparer.Ordinal)];

    public static TheoryData<string> DiffCaseIds() =>
        [.. Cases.Where(c => c.Value.TryGetProperty("edited", out _)).Select(c => c.Key).Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void Resolve_SharedVector_MatchesExpectedResolved(string id)
    {
        var (c, recipe, variants, products) = Load(id);
        var expected = c.GetProperty("expectedResolved");

        foreach (var variant in variants)
        {
            var actual = VariantVectorJson.WriteResolved(_engine.Resolve(recipe, variant), products);
            var diff = VariantVectorJson.FirstDifference(JsonNode.Parse(expected.GetProperty(variant.Id.ToString()).GetRawText()), actual);
            diff.ShouldBeNull($"{id} / {variant.Name}: {diff}");
        }
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void Resolve_NullVariant_ReturnsTheBase(string id)
    {
        var (c, recipe, _, products) = Load(id);
        var b = c.GetProperty("base");
        var expected = new JsonObject
        {
            ["ingredients"] = JsonNode.Parse(b.GetProperty("ingredients").GetRawText()),
            ["prepSteps"] = JsonNode.Parse(b.GetProperty("prepSteps").GetRawText()),
            ["instructionSteps"] = JsonNode.Parse(b.GetProperty("instructionSteps").GetRawText()),
            ["utensilIds"] = JsonNode.Parse(b.GetProperty("utensilIds").GetRawText()),
            ["prepMinutes"] = JsonNode.Parse(b.GetProperty("prepMinutes").GetRawText()),
            ["cookMinutes"] = JsonNode.Parse(b.GetProperty("cookMinutes").GetRawText()),
        };

        var diff = VariantVectorJson.FirstDifference(expected, VariantVectorJson.WriteResolved(_engine.Resolve(recipe, null), products));
        diff.ShouldBeNull($"{id}: {diff}");
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void BuildMatrix_SharedVector_MatchesExpectedMatrix(string id)
    {
        var (c, recipe, variants, products) = Load(id);

        var actual = VariantVectorJson.WriteMatrix(_engine.BuildMatrix(recipe, variants), c, products);

        var diff = VariantVectorJson.FirstDifference(JsonNode.Parse(c.GetProperty("expectedMatrix").GetRawText()), actual);
        diff.ShouldBeNull($"{id}: {diff}");
    }

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void BuildMatrix_SharedVector_EveryVariantInExactlyOneOptionPerSlot(string id)
    {
        var (_, recipe, variants, _) = Load(id);

        var matrix = _engine.BuildMatrix(recipe, variants);

        var columns = matrix.Variants.Select(v => v.VariantId).ToList();
        foreach (var slot in matrix.Slots)
        {
            slot.Options.SelectMany(o => o.VariantIds).ShouldBe(columns, ignoreOrder: true, $"{id} {slot.Key}");
            slot.Options.Select(o => o.Key).ShouldBeUnique($"{id} {slot.Key}");
        }
    }

    [Theory]
    [MemberData(nameof(DiffCaseIds))]
    public void Diff_SharedVectorWithEdited_ReproducesTheVariantOps(string id)
    {
        var (c, recipe, _, products) = Load(id);

        foreach (var edited in c.GetProperty("edited").EnumerateObject())
        {
            var variant = c.GetProperty("variants").EnumerateArray().Single(v => v.GetProperty("id").GetString() == edited.Name);
            var expected = new JsonObject();
            foreach (var key in new[] { "ingredientOps", "stepOps", "utensilIdsAdded", "utensilIdsRemoved", "prepMinutes", "cookMinutes" })
            {
                expected[key] = JsonNode.Parse(variant.GetProperty(key).GetRawText());
            }

            var actual = VariantVectorJson.WriteDiff(_engine.Diff(recipe, VariantVectorJson.ReadDraft(edited.Value, products)));

            var diff = VariantVectorJson.FirstDifference(expected, actual);
            diff.ShouldBeNull($"{id} / {edited.Name}: {diff}");
        }
    }

    [Fact]
    public void Vectors_B4File_HasAtLeastTenCasesAndSomeDiffCases()
    {
        Cases.Keys.Count(k => k.StartsWith("B4-", StringComparison.Ordinal)).ShouldBeGreaterThanOrEqualTo(10);
        DiffCaseIds().Count.ShouldBeGreaterThanOrEqualTo(5);
    }

    private static (JsonElement Case, Core.Recipes.Recipe Recipe, List<Variant> Variants, Dictionary<Guid, JsonElement> Products) Load(string id)
    {
        var c = Cases[id];
        var recipe = VariantVectorJson.ReadBase(c.GetProperty("base"));
        var variants = c.GetProperty("variants").EnumerateArray().Select(v => VariantVectorJson.ReadVariant(v, recipe.Id)).ToList();
        return (c, recipe, variants, VariantVectorJson.CollectProducts(c));
    }
}
