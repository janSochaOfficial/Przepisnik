using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Transfer;
using RecipesList.Core.Variants;

namespace RecipesList.UnitTests.Variants;

/// <summary>Servings (PROJECT §5.7, D-37): validation, the variant override in diff and resolve, and import alignment.</summary>
public sealed class ServingsTests
{
    private static readonly Guid Flour = RecipeBuilder.NewProduct();

    private readonly VariantEngine _engine = new();

    [Theory]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(100, true)]
    [InlineData(0, false)]
    [InlineData(101, false)]
    [InlineData(-3, false)]
    public void RecipeValidation_AcceptsServingsFromOneToOneHundred(int? servings, bool valid)
    {
        var input = new RecipeWriteInput(
            "Naleśniki", null, null, null, null, servings, [], [],
            [new IngredientWriteInput(null, Flour, new Quantity(200, Unit.G), null, false)],
            [],
            [new StepWriteInput(null, [new TextBlock("Smaż.")])]);

        var errors = RecipeInputValidator.Validate(input);

        if (valid)
        {
            errors.Any.ShouldBeFalse();
        }
        else
        {
            Should.Throw<DomainValidationException>(errors.ThrowIfAny).Errors["servings"].ShouldBe(["out_of_range"]);
        }
    }

    [Fact]
    public void Diff_SameServings_IsNoOp()
    {
        var recipe = Base(4);

        _engine.Diff(recipe, RecipeBuilder.DraftOf(recipe)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Diff_DifferentServings_IsAnOverride()
    {
        var recipe = Base(4);

        var diff = _engine.Diff(recipe, RecipeBuilder.DraftOf(recipe) with { Servings = 6 });

        diff.ServingsOverride.ShouldBe(6);
        diff.IsEmpty.ShouldBeFalse();
    }

    [Fact]
    public void Diff_ServingsOnABaseWithout_IsAnOverride()
    {
        var recipe = Base(null);

        _engine.Diff(recipe, RecipeBuilder.DraftOf(recipe) with { Servings = 2 }).ServingsOverride.ShouldBe(2);
    }

    [Fact]
    public void Diff_ClearedServings_InheritsTheBase()
    {
        var recipe = Base(4);

        var diff = _engine.Diff(recipe, RecipeBuilder.DraftOf(recipe) with { Servings = null });

        diff.ServingsOverride.ShouldBeNull();
        diff.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_Override_WinsOverTheBase_NullInherits()
    {
        var recipe = Base(4);
        var overriding = new Variant { RecipeId = recipe.Id, Name = "Na imprezę", ServingsOverride = 10 };
        var inheriting = new Variant { RecipeId = recipe.Id, Name = "Ostrzejsza" };

        _engine.Resolve(recipe, null).Servings.ShouldBe(4);
        _engine.Resolve(recipe, overriding).Servings.ShouldBe(10);
        _engine.Resolve(recipe, inheriting).Servings.ShouldBe(4);
    }

    [Fact]
    public void ChangeCount_CountsTheServingsOverride()
    {
        var variant = new Variant { Name = "x", ServingsOverride = 2 };

        VariantSummaryRow.ChangeCount(variant).ShouldBe(1);
    }

    [Theory]
    [InlineData(4, 6, 6)]
    [InlineData(4, null, null)]
    [InlineData(null, 3, 3)]
    [InlineData(4, 4, null)]
    public void ImportAlignment_ServingsDifferingInTheFile_BecomeAnOverride(int? target, int? file, int? expectedOverride)
    {
        var targetRecipe = Base(target);
        var source = Base(file);

        var draft = ImportAlignment.Align(_engine.Resolve(targetRecipe, null), _engine.Resolve(source, null));
        var diff = _engine.Diff(targetRecipe, draft);

        diff.ServingsOverride.ShouldBe(expectedOverride);
        _engine.Resolve(targetRecipe, RecipeBuilder.VariantOf(targetRecipe, diff)).Servings.ShouldBe(file ?? target);
    }

    [Fact]
    public void HasSameContent_ComparesServings()
    {
        var four = _engine.Resolve(Base(4), null);
        var six = four with { Servings = 6 };

        ImportAlignment.HasSameContent(four, four).ShouldBeTrue();
        ImportAlignment.HasSameContent(four, six).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void ExportValidator_RejectsAVariantOverrideOutOfRange(int servings)
    {
        var document = Document(servingsOverride: servings);

        var error = Should.Throw<DomainValidationException>(() => ExportValidator.EnsureValid(document));

        error.Code.ShouldBe(ErrorCodes.ImportInvalidFormat);
        error.Errors.ShouldContainKey("variants[0].servingsOverride");
    }

    [Fact]
    public void ExportValidator_AcceptsAVariantOverrideInRange()
    {
        Should.NotThrow(() => ExportValidator.EnsureValid(Document(servingsOverride: 8)));
    }

    private static Recipe Base(int? servings) =>
        new RecipeBuilder().Ingredient(Flour, 200).Step(StepSection.Instructions, "Wymieszaj.").Servings(servings).Recipe;

    private static RecipeExportDocument Document(int? servingsOverride) => new()
    {
        Format = ExportFormat.Name,
        FormatVersion = ExportFormat.Version,
        ExportedAt = DateTimeOffset.UnixEpoch,
        Products = [],
        Images = new Dictionary<string, ExportImage>(),
        Recipe = new ExportRecipe
        {
            Key = "r1",
            Title = "Naleśniki",
            AuthorNickname = "ala",
            Servings = 4,
            Tags = [],
            Utensils = [],
            Ingredients = [],
            PrepSteps = [],
            InstructionSteps = [new ExportStep { Key = "s1", Blocks = [new ExportTextBlock("Smaż.")] }],
        },
        Variants =
        [
            new ExportVariant
            {
                Name = "Na imprezę",
                AuthorNickname = "ala",
                ServingsOverride = servingsOverride,
                UtensilsAdded = [],
                UtensilsRemoved = [],
                IngredientOps = [],
                StepOps = [],
            },
        ],
    };
}
