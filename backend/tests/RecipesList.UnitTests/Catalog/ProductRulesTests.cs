using RecipesList.Core.Catalog;
using RecipesList.Core.Common;

namespace RecipesList.UnitTests.Catalog;

public sealed class ProductRulesTests
{
    private static ProductInput Input(
        string name = "Cebula",
        MeasureKind kind = MeasureKind.Count,
        DisplayClass display = DisplayClass.Piece,
        bool sizes = false,
        bool half = false) =>
        new(name, ProductCategory.Vegetables, kind, display, sizes, half, null, null, null, null);

    [Theory]
    [InlineData(MeasureKind.Mass, DisplayClass.Weight)]
    [InlineData(MeasureKind.Volume, DisplayClass.Liquid)]
    [InlineData(MeasureKind.Volume, DisplayClass.Spice)]
    [InlineData(MeasureKind.Count, DisplayClass.Piece)]
    public void FittingClasses_AreValid(MeasureKind kind, DisplayClass display) =>
        Should.NotThrow(() => ProductRules.Validate(Input(kind: kind, display: display)));

    [Fact]
    public void SizesAndHalf_AreValidForCount() =>
        Should.NotThrow(() => ProductRules.Validate(Input(sizes: true, half: true)));

    [Theory]
    [InlineData(MeasureKind.Mass, DisplayClass.Liquid, false, false)]
    [InlineData(MeasureKind.Mass, DisplayClass.Piece, false, false)]
    [InlineData(MeasureKind.Volume, DisplayClass.Weight, false, false)]
    [InlineData(MeasureKind.Count, DisplayClass.Spice, false, false)]
    [InlineData(MeasureKind.Mass, DisplayClass.Weight, true, false)]
    [InlineData(MeasureKind.Volume, DisplayClass.Liquid, false, true)]
    public void BadCombinations_AreRejected(MeasureKind kind, DisplayClass display, bool sizes, bool half)
    {
        var e = Should.Throw<DomainValidationException>(() => ProductRules.Validate(Input(kind: kind, display: display, sizes: sizes, half: half)));
        e.Code.ShouldBe(ErrorCodes.ProductInvalidCombination);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" a ")]
    public void ShortNames_AreRejected(string name) =>
        Should.Throw<DomainValidationException>(() => ProductRules.Validate(Input(name))).Code.ShouldBe(ErrorCodes.ValidationFailed);

    [Fact]
    public void LongNames_AreRejected() =>
        Should.Throw<DomainValidationException>(() => ProductRules.Validate(Input(new string('a', 81))));

    [Fact]
    public void CleanName_CollapsesWhitespace() =>
        ProductRules.CleanName("  Śmietana \t 18%  ").ShouldBe("Śmietana 18%");
}
