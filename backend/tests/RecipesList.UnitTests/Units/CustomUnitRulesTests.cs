using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Units;

namespace RecipesList.UnitTests.Units;

public sealed class CustomUnitRulesTests
{
    private static readonly PluralForms Can = new("puszka", "puszki", "puszek", "puszki");
    private readonly QuantityConverter _converter = new();

    [Fact]
    public void Define_ProductUnit_UsesTheBaseAmountInTheProductKind()
    {
        var butter = Product(MeasureKind.Mass, gramsPerMl: 0.9m);

        var definition = CustomUnitRules.Define(new CustomUnitInput("  Kostka  ", Can, butter.Id, new Quantity(1, Unit.Glass)), butter, _converter);

        definition.Name.ShouldBe("Kostka");
        definition.NormalizedName.ShouldBe("kostka");
        definition.MeasureKind.ShouldBe(MeasureKind.Mass);
        definition.Amount.ShouldBe(225m);
    }

    [Fact]
    public void Define_UnitForEveryProduct_UsesTheNativeAmountAndIgnoresSizes()
    {
        var definition = CustomUnitRules.Define(new CustomUnitInput("pęczek", Can, null, new Quantity(2, Unit.Piece, PieceSize.Large)), null, _converter);

        definition.MeasureKind.ShouldBe(MeasureKind.Count);
        definition.Amount.ShouldBe(2m);
    }

    [Fact]
    public void Define_ProductUnitThatCannotConvert_IsUnitInvalid()
    {
        var flour = Product(MeasureKind.Mass);

        var error = Should.Throw<DomainValidationException>(
            () => CustomUnitRules.Define(new CustomUnitInput("kubek", Can, flour.Id, new Quantity(1, Unit.Glass)), flour, _converter));

        error.Code.ShouldBe(ErrorCodes.UnitInvalid);
    }

    [Theory]
    [InlineData("", "name")]
    [InlineData("12345678901234567890123456789012345678901", "name")]
    public void Define_BadName_IsValidationFailed(string name, string path)
    {
        var error = Should.Throw<DomainValidationException>(
            () => CustomUnitRules.Define(new CustomUnitInput(name, Can, null, new Quantity(1, Unit.G)), null, _converter));

        error.Code.ShouldBe(ErrorCodes.ValidationFailed);
        error.Errors.ShouldContainKey(path);
    }

    [Fact]
    public void Define_MissingFormsOrEquals_IsValidationFailed()
    {
        var error = Should.Throw<DomainValidationException>(
            () => CustomUnitRules.Define(new CustomUnitInput("garść", null, null, null), null, _converter));

        error.Errors.Keys.ShouldBe(["forms", "equals"], ignoreOrder: true);
    }

    [Fact]
    public void Define_EqualsInACustomUnitOrWithAWrongSize_IsValidationFailed()
    {
        var custom = Should.Throw<DomainValidationException>(
            () => CustomUnitRules.Define(new CustomUnitInput("garść", Can, null, new Quantity(1, Unit.Custom, null, Guid.CreateVersion7())), null, _converter));
        var size = Should.Throw<DomainValidationException>(
            () => CustomUnitRules.Define(new CustomUnitInput("garść", Can, null, new Quantity(1, Unit.G, PieceSize.Large)), null, _converter));
        var zero = Should.Throw<DomainValidationException>(
            () => CustomUnitRules.Define(new CustomUnitInput("garść", Can, null, new Quantity(0, Unit.G)), null, _converter));

        custom.Errors.ShouldContainKey("equals.unit");
        size.Errors.ShouldContainKey("equals");
        zero.Errors.ShouldContainKey("equals");
    }

    [Fact]
    public void Define_EmptyForm_IsValidationFailed()
    {
        var error = Should.Throw<DomainValidationException>(
            () => CustomUnitRules.Define(new CustomUnitInput("garść", Can with { Fraction = " " }, null, new Quantity(30, Unit.G)), null, _converter));

        error.Errors.ShouldContainKey("forms.fraction");
    }

    [Theory]
    [InlineData(400, 403.9, true)]
    [InlineData(400, 404.1, false)]
    [InlineData(150, 148.6, true)]
    public void IsSameAmount_IsWithinOnePercent(decimal a, decimal b, bool expected)
    {
        CustomUnitRules.IsSameAmount(a, b).ShouldBe(expected);
    }

    [Fact]
    public void UniqueName_AddsTheFirstFreeNumber()
    {
        CustomUnitRules.UniqueName("Puszka", new HashSet<string> { "inna" }).ShouldBe("Puszka");
        CustomUnitRules.UniqueName("Puszka", new HashSet<string> { "puszka", "puszka (2)" }).ShouldBe("Puszka (3)");
    }

    [Fact]
    public void UniqueName_LongName_StaysWithinTheLimit()
    {
        var name = new string('a', CustomUnitRules.MaxName);

        var unique = CustomUnitRules.UniqueName(name, new HashSet<string> { name });

        unique.Length.ShouldBe(CustomUnitRules.MaxName);
        unique.ShouldEndWith(" (2)");
    }

    [Fact]
    public void ToBaseAmount_CustomQuantityWithoutItsUnit_IsNull()
    {
        var product = Product(MeasureKind.Mass);
        var unit = MakeUnit(product.Id, 400);

        _converter.ToBaseAmount(new Quantity(2, Unit.Custom, null, unit.Id), product).ShouldBeNull();
        _converter.ToBaseAmount(new Quantity(2, Unit.Custom, null, Guid.CreateVersion7()), product, unit).ShouldBeNull();
        _converter.ToBaseAmount(new Quantity(2, Unit.Custom, null, unit.Id), product, unit).ShouldBe(800m);
    }

    [Theory]
    [InlineData(1, Unit.G, null, false, true)]
    [InlineData(0, Unit.G, null, false, false)]
    [InlineData(1, Unit.G, PieceSize.Large, false, false)]
    [InlineData(1, Unit.Piece, PieceSize.Large, false, true)]
    [InlineData(1, Unit.Custom, null, true, true)]
    [InlineData(1, Unit.Custom, null, false, false)]
    [InlineData(1, Unit.Custom, PieceSize.Small, true, false)]
    [InlineData(1, Unit.Ml, null, true, false)]
    public void Quantity_IsWellFormed(decimal value, Unit unit, PieceSize? size, bool withUnitId, bool expected)
    {
        new Quantity(value, unit, size, withUnitId ? Guid.CreateVersion7() : null).IsWellFormed.ShouldBe(expected);
    }

    private static CustomUnit MakeUnit(Guid? productId, decimal amount) => new()
    {
        Name = "puszka",
        NormalizedName = "puszka",
        Forms = Can,
        ProductId = productId,
        MeasureKind = MeasureKind.Mass,
        Amount = amount,
    };

    private static Product Product(MeasureKind kind, decimal? gramsPerMl = null) => new()
    {
        Name = "test",
        NormalizedName = "test",
        MeasureKind = kind,
        GramsPerMl = gramsPerMl,
    };
}
