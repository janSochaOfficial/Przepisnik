using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;

namespace RecipesList.UnitTests.Recipes;

public sealed class RecipeInputValidatorTests
{
    private static readonly Guid Product = Guid.NewGuid();

    private static RecipeWriteInput Valid() => new(
        "Naleśniki", null, null, null, null, [], [],
        [new IngredientWriteInput(null, Product, new Quantity(200, Unit.G), null, false)],
        [],
        [new StepWriteInput(null, [new TextBlock("Smaż.")])]);

    private static DomainValidationException Fail(RecipeWriteInput input) =>
        Should.Throw<DomainValidationException>(() => RecipeInputValidator.Validate(input).ThrowIfAny());

    [Fact]
    public void Validate_ValidInput_HasNoErrors()
    {
        Should.NotThrow(() => RecipeInputValidator.Validate(Valid()).ThrowIfAny());
    }

    [Fact]
    public void Validate_BlankTitle_ReportsTitle()
    {
        var error = Fail(Valid() with { Title = "  " });

        error.Code.ShouldBe(ErrorCodes.ValidationFailed);
        error.Errors.ShouldContainKey("title");
    }

    [Fact]
    public void Validate_NoInstructionSteps_ReportsInstructionSteps()
    {
        Fail(Valid() with { InstructionSteps = [] }).Errors.ShouldContainKey("instructionSteps");
    }

    [Fact]
    public void Validate_NonPositiveQuantity_UsesTheQuantityInvalidCodeAtTheIngredientPath()
    {
        var input = Valid() with { Ingredients = [new IngredientWriteInput(null, Product, new Quantity(0, Unit.G), null, false)] };

        var error = Fail(input);

        error.Code.ShouldBe(ErrorCodes.QuantityInvalid);
        error.Errors["ingredients[0].quantity"].ShouldBe([ErrorCodes.QuantityInvalid]);
    }

    [Fact]
    public void Validate_SizeOnANonPieceUnit_IsInvalid()
    {
        var input = Valid() with { Ingredients = [new IngredientWriteInput(null, Product, new Quantity(1, Unit.G, PieceSize.Large), null, false)] };

        Fail(input).Code.ShouldBe(ErrorCodes.QuantityInvalid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2881)]
    public void Validate_MinutesOutOfRange_Reported(int minutes)
    {
        Fail(Valid() with { CookMinutes = minutes }).Errors.ShouldContainKey("cookMinutes");
    }

    [Fact]
    public void Validate_StepLimits_ReportBlockPaths()
    {
        var input = Valid() with
        {
            PrepSteps = [new StepWriteInput(null, []), new StepWriteInput(null, [new TextBlock(new string('x', 5001))])],
        };

        var errors = Fail(input).Errors;

        errors.ShouldContainKey("prepSteps[0].blocks");
        errors.ShouldContainKey("prepSteps[1].blocks[0].text");
    }

    [Fact]
    public void Validate_TooManyIngredients_Reported()
    {
        var rows = Enumerable.Range(0, 101).Select(_ => new IngredientWriteInput(null, Product, null, null, false)).ToList();

        Fail(Valid() with { Ingredients = rows }).Errors.ShouldContainKey("ingredients");
    }

    [Fact]
    public void ThrowIfAny_MixedFieldCodes_FallsBackToValidationFailed()
    {
        var input = Valid() with
        {
            Title = "",
            Ingredients = [new IngredientWriteInput(null, Product, new Quantity(-1, Unit.G), null, false)],
        };

        Fail(input).Code.ShouldBe(ErrorCodes.ValidationFailed);
    }
}
