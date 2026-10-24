using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Transfer;

namespace RecipesList.UnitTests.Transfer;

public sealed class ExportValidatorTests
{
    [Fact]
    public void EnsureValid_ConsistentFile_Passes()
    {
        Should.NotThrow(() => ExportValidator.EnsureValid(File()));
    }

    [Fact]
    public void EnsureValid_UnknownFormat_IsInvalidFormat()
    {
        var error = Should.Throw<DomainValidationException>(() => ExportValidator.EnsureValid(File() with { Format = "other" }));

        error.Code.ShouldBe(ErrorCodes.ImportInvalidFormat);
    }

    [Fact]
    public void EnsureValid_VersionTwo_IsUnsupportedVersion()
    {
        var error = Should.Throw<DomainValidationException>(() => ExportValidator.EnsureValid(File() with { FormatVersion = 2 }));

        error.Code.ShouldBe(ErrorCodes.ImportUnsupportedVersion);
    }

    [Fact]
    public void EnsureValid_OpPointingAtAMissingKey_ReportsThePath()
    {
        var variant = Variant(new ExportRemoveIngredientOp("i404"));

        var error = Invalid(File() with { Variants = [variant] });

        error.Errors.ShouldContainKey("variants[0].ingredientOps[0].targetKey");
    }

    [Fact]
    public void EnsureValid_OpAfterAnAddOfTheSameVariant_Resolves()
    {
        var variant = Variant(
            new ExportAddIngredientOp("i2", "i1", "p1", null, null, false),
            new ExportAddIngredientOp("i3", "i2", "p1", null, null, false));

        Should.NotThrow(() => ExportValidator.EnsureValid(File() with { Variants = [variant] }));
    }

    [Fact]
    public void EnsureValid_AddAfterAnAddOfAnotherVariant_DoesNotResolve()
    {
        var first = Variant(new ExportAddIngredientOp("i2", "i1", "p1", null, null, false));
        var second = Variant(new ExportAddIngredientOp("i3", "i2", "p1", null, null, false));

        var error = Invalid(File() with { Variants = [first, second] });

        error.Errors.ShouldContainKey("variants[1].ingredientOps[0].afterKey");
    }

    [Fact]
    public void EnsureValid_StepOpInTheWrongSection_DoesNotResolve()
    {
        var variant = Variant(stepOps: [new ExportRemoveStepOp(StepSection.Instructions, "s1")]);

        var error = Invalid(File() with { Variants = [variant] });

        error.Errors.ShouldContainKey("variants[0].stepOps[0].targetKey");
    }

    [Fact]
    public void EnsureValid_ImageKeyWithoutImage_IsReported()
    {
        var recipe = File().Recipe with { CoverImageKey = "img9" };

        Invalid(File() with { Recipe = recipe }).Errors.ShouldContainKey("recipe.coverImageKey");
    }

    [Fact]
    public void EnsureValid_TwoProductsWithTheSameNormalizedName_AreReported()
    {
        var twin = Product("p2", "MĄKA");

        Invalid(File() with { Products = [.. File().Products, twin] }).Errors.ShouldContainKey("products[1].name");
    }

    [Fact]
    public void EnsureValid_ImageThatIsNotBase64_IsReported()
    {
        var images = new Dictionary<string, ExportImage> { ["img1"] = new() { ContentType = "image/webp", DataBase64 = "***" } };

        Invalid(File() with { Images = images }).Errors.ShouldContainKey("images.img1.dataBase64");
    }

    [Fact]
    public void ReferencedImageKeys_ListsCoverStepsAndVariantOpsOnce()
    {
        var recipe = File().Recipe with
        {
            CoverImageKey = "img1",
            PrepSteps = [new ExportStep { Key = "s1", Blocks = [new ExportTextBlock("a"), new ExportImageBlock("img2")] }],
        };
        var variant = Variant(stepOps: [new ExportAddStepOp(StepSection.Instructions, "s9", null, [new ExportImageBlock("img3"), new ExportImageBlock("img1")])]);

        ExportValidator.ReferencedImageKeys(File() with { Recipe = recipe, Variants = [variant] }).ShouldBe(["img1", "img2", "img3"]);
    }

    private static DomainValidationException Invalid(RecipeExportDocument document) =>
        Should.Throw<DomainValidationException>(() => ExportValidator.EnsureValid(document));

    private static ExportVariant Variant(params ExportIngredientOp[] ingredientOps) => Variant(ingredientOps, []);

    private static ExportVariant Variant(ExportIngredientOp[]? ingredientOps = null, ExportStepOp[]? stepOps = null) => new()
    {
        Name = "Wariant",
        AuthorNickname = "bartek",
        UtensilsAdded = [],
        UtensilsRemoved = [],
        IngredientOps = ingredientOps ?? [],
        StepOps = stepOps ?? [],
    };

    private static ExportProduct Product(string key, string name) => new()
    {
        Key = key,
        Name = name,
        Category = ProductCategory.DryGoods,
        MeasureKind = MeasureKind.Mass,
        DisplayClass = DisplayClass.Weight,
        HasSizes = false,
        AllowHalf = false,
    };

    // One ingredient (i1), a prep step (s1) and an instruction step (s2); images img1..img3 exist.
    private static RecipeExportDocument File() => new()
    {
        Format = ExportFormat.Name,
        FormatVersion = ExportFormat.Version,
        ExportedAt = new DateTimeOffset(2026, 10, 24, 10, 0, 0, TimeSpan.Zero),
        Products = [Product("p1", "Mąka")],
        Images = new Dictionary<string, ExportImage>
        {
            ["img1"] = Image(),
            ["img2"] = Image(),
            ["img3"] = Image(),
        },
        Recipe = new ExportRecipe
        {
            Key = "r1",
            Title = "Placki",
            AuthorNickname = "ania",
            Tags = [],
            Utensils = [],
            Ingredients = [new ExportIngredient { Key = "i1", ProductKey = "p1", Quantity = new Quantity(200, Unit.G), Optional = false }],
            PrepSteps = [new ExportStep { Key = "s1", Blocks = [new ExportTextBlock("Przygotuj.")] }],
            InstructionSteps = [new ExportStep { Key = "s2", Blocks = [new ExportTextBlock("Smaż.")] }],
        },
        Variants = [],
    };

    private static ExportImage Image() => new() { ContentType = ExportFormat.ImageContentType, DataBase64 = "UklGRg==" };
}
