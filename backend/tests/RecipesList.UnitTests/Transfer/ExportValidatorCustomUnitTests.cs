using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Transfer;

namespace RecipesList.UnitTests.Transfer;

/// <summary>The custom-unit part of the export format (API.md §8, PROJECT §5.6).</summary>
public sealed class ExportValidatorCustomUnitTests
{
    [Fact]
    public void EnsureValid_KeyedCustomUnit_Passes()
    {
        Should.NotThrow(() => ExportValidator.EnsureValid(File(new ExportQuantity(2, Unit.Custom, null, "u1"))));
    }

    [Fact]
    public void EnsureValid_UnknownUnitKey_ReportsThePath()
    {
        var error = Invalid(File(new ExportQuantity(2, Unit.Custom, null, "u9")));

        error.Errors.ShouldContainKey("recipe.ingredients[0].quantity.customUnitKey");
    }

    [Fact]
    public void EnsureValid_CustomUnitWithoutKey_ReportsThePath()
    {
        var error = Invalid(File(new ExportQuantity(2, Unit.Custom)));

        error.Errors.ShouldContainKey("recipe.ingredients[0].quantity.customUnitKey");
    }

    [Fact]
    public void EnsureValid_UnitKeyOnABuiltInUnit_IsInvalid()
    {
        var error = Invalid(File(new ExportQuantity(200, Unit.G, null, "u1")));

        error.Errors["recipe.ingredients[0].quantity.customUnitKey"].ShouldBe(["invalid"]);
    }

    [Fact]
    public void EnsureValid_UnitOfAnUnknownProductOrWithoutAnAmount_ReportsBoth()
    {
        var file = File(new ExportQuantity(2, Unit.Custom, null, "u1"));
        file = file with { CustomUnits = [file.CustomUnits![0] with { ProductKey = "p9", Amount = 0 }] };

        var error = Invalid(file);

        error.Errors.ShouldContainKey("customUnits[0].productKey");
        error.Errors.ShouldContainKey("customUnits[0].amount");
    }

    [Fact]
    public void EnsureValid_CustomUnitInAVariantOp_IsChecked()
    {
        var file = File(new ExportQuantity(200, Unit.G)) with
        {
            Variants =
            [
                new ExportVariant
                {
                    Name = "Wariant",
                    AuthorNickname = "ania",
                    UtensilsAdded = [],
                    UtensilsRemoved = [],
                    IngredientOps = [new ExportModifyIngredientOp("i1", "p1", new ExportQuantity(1, Unit.Custom, null, "u7"), null, false)],
                    StepOps = [],
                },
            ],
        };

        var error = Invalid(file);

        error.Errors.ShouldContainKey("variants[0].ingredientOps[0].quantity.customUnitKey");
    }

    private static DomainValidationException Invalid(RecipeExportDocument file)
    {
        var error = Should.Throw<DomainValidationException>(() => ExportValidator.EnsureValid(file));
        error.Code.ShouldBe(ErrorCodes.ImportInvalidFormat);
        return error;
    }

    private static RecipeExportDocument File(ExportQuantity quantity) => new()
    {
        Format = ExportFormat.Name,
        FormatVersion = ExportFormat.Version,
        ExportedAt = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero),
        Products =
        [
            new ExportProduct
            {
                Key = "p1",
                Name = "Pomidory krojone",
                Category = ProductCategory.Canned,
                MeasureKind = MeasureKind.Mass,
                DisplayClass = DisplayClass.Weight,
                HasSizes = false,
                AllowHalf = false,
            },
        ],
        CustomUnits =
        [
            new ExportCustomUnit
            {
                Key = "u1",
                Name = "puszka",
                Forms = new PluralForms("puszka", "puszki", "puszek", "puszki"),
                ProductKey = "p1",
                MeasureKind = MeasureKind.Mass,
                Amount = 400,
            },
        ],
        Images = new Dictionary<string, ExportImage>(),
        Recipe = new ExportRecipe
        {
            Key = "r1",
            Title = "Sos",
            AuthorNickname = "ania",
            Tags = [],
            Utensils = [],
            Ingredients = [new ExportIngredient { Key = "i1", ProductKey = "p1", Quantity = quantity, Optional = false }],
            PrepSteps = [],
            InstructionSteps = [new ExportStep { Key = "s1", Blocks = [new ExportTextBlock("Gotuj.")] }],
        },
        Variants = [],
    };
}
