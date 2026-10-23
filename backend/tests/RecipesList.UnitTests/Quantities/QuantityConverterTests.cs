using System.Text.Json;
using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;

namespace RecipesList.UnitTests.Quantities;

/// <summary>Driven by every file in <c>shared/test-vectors/quantities/</c> (the frontend asserts the same cases).</summary>
public sealed class QuantityConverterTests
{
    private const decimal Tolerance = 0.0001m;
    private static readonly IReadOnlyDictionary<string, JsonElement> Cases = TestVectors.Load("quantities");
    private readonly QuantityConverter _converter = new();

    public static TheoryData<string> CaseIds() => [.. Cases.Keys.Order(StringComparer.Ordinal)];

    [Theory]
    [MemberData(nameof(CaseIds))]
    public void ToBaseAmount_SharedVector_MatchesExpected(string id)
    {
        var c = Cases[id];
        var quantity = ReadQuantity(c.GetProperty("quantity"));
        var product = ReadProduct(c.GetProperty("product"));
        var expected = c.GetProperty("expected");

        var actual = _converter.ToBaseAmount(quantity, product);

        if (expected.ValueKind == JsonValueKind.Null)
        {
            actual.ShouldBeNull(id);
        }
        else
        {
            actual.ShouldNotBeNull(id);
            Math.Abs(actual.Value - expected.GetDecimal()).ShouldBeLessThanOrEqualTo(Tolerance, id);
        }
    }

    [Fact]
    public void ToBaseAmount_NullQuantity_ReturnsNull()
    {
        _converter.ToBaseAmount(null, ReadProduct(JsonDocument.Parse("""{ "measureKind": "mass", "hasSizes": false }""").RootElement))
            .ShouldBeNull();
    }

    [Fact]
    public void ToBaseAmount_Result_IsRoundedToFourDecimals()
    {
        var oil = new Product { Name = "olej", NormalizedName = "olej", MeasureKind = MeasureKind.Volume, GramsPerMl = 0.92m };

        _converter.ToBaseAmount(new Quantity(100m, Unit.G), oil).ShouldBe(108.6957m);
    }

    private static Quantity ReadQuantity(JsonElement q)
    {
        var size = q.TryGetProperty("size", out var s) && s.ValueKind == JsonValueKind.String
            ? Enum.Parse<PieceSize>(s.GetString()!, ignoreCase: true)
            : (PieceSize?)null;
        return new Quantity(q.GetProperty("value").GetDecimal(), Enum.Parse<Unit>(q.GetProperty("unit").GetString()!, ignoreCase: true), size);
    }

    private static Product ReadProduct(JsonElement p) => new()
    {
        Name = "test",
        NormalizedName = "test",
        MeasureKind = Enum.Parse<MeasureKind>(p.GetProperty("measureKind").GetString()!, ignoreCase: true),
        HasSizes = p.TryGetProperty("hasSizes", out var hasSizes) && hasSizes.GetBoolean(),
        GramsPerMl = OptionalDecimal(p, "gramsPerMl"),
        GramsPerPiece = OptionalDecimal(p, "gramsPerPiece"),
    };

    private static decimal? OptionalDecimal(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
}
