using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;

namespace RecipesList.UnitTests.Variants;

/// <summary>ImportAlignment (PROJECT §4.7, D-35): the edited copy of a target recipe that has an imported recipe's content.</summary>
public sealed class ImportAlignmentTests
{
    private static readonly Guid Salt = RecipeBuilder.NewProduct();
    private static readonly Guid Flour = RecipeBuilder.NewProduct();
    private static readonly Guid Milk = RecipeBuilder.NewProduct();
    private static readonly Guid Eggs = RecipeBuilder.NewProduct();
    private static readonly Guid Pan = Guid.CreateVersion7();
    private static readonly Guid Oven = Guid.CreateVersion7();

    private readonly VariantEngine _engine = new();

    [Fact]
    public void IdenticalRecipe_GivesAnEmptyDiff()
    {
        var target = new RecipeBuilder().Ingredient(Flour, 200).Ingredient(Milk, 300).Step(StepSection.Instructions, "Wymieszaj.").Utensil(Pan).Times(5, 10).Recipe;
        var source = new RecipeBuilder().Ingredient(Flour, 200).Ingredient(Milk, 300).Step(StepSection.Instructions, "Wymieszaj.").Utensil(Pan).Times(5, 10).Recipe;

        var draft = Align(target, source);

        _engine.Diff(target, draft).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void ChangedAmountExtraAndMissingRowAndRewordedSteps_ResolveToTheSource()
    {
        var target = new RecipeBuilder()
            .Ingredient(Flour, 200).Ingredient(Milk, 300).Ingredient(Salt, 1)
            .Step(StepSection.Prep, "Odmierz.")
            .Step(StepSection.Instructions, "Wymieszaj.").Step(StepSection.Instructions, "Smaż.")
            .Recipe;
        var source = new RecipeBuilder()
            .Ingredient(Flour, 250).Ingredient(Eggs, 2).Ingredient(Milk, 300)
            .Step(StepSection.Prep, "Odmierz.")
            .Step(StepSection.Instructions, "Wymieszaj dokładnie.").Step(StepSection.Instructions, "Smaż na złoto.").Step(StepSection.Instructions, "Podawaj.")
            .Recipe;

        var resolved = ResolveAligned(target, source);

        ImportAlignment.HasSameContent(resolved, _engine.Resolve(source, null)).ShouldBeTrue();
        resolved.Ingredients[0].Id.ShouldBe(target.Ingredients[0].Id);
        resolved.Ingredients[2].Id.ShouldBe(target.Ingredients[1].Id);
        resolved.InstructionSteps.Take(2).Select(s => s.Id).ShouldBe(target.Steps.Where(s => s.Section == StepSection.Instructions).Select(s => s.Id));
    }

    [Fact]
    public void LcsTie_KeepsTheEarliestTargetRow()
    {
        var target = new RecipeBuilder().Ingredient(out var flour, Flour, 200).Ingredient(Milk, 300).Recipe;
        var source = new RecipeBuilder().Ingredient(Milk, 300).Ingredient(Flour, 200).Recipe;

        var draft = Align(target, source);

        draft.Ingredients.Select(i => i.Id).ShouldBe([null, flour]);
        ImportAlignment.HasSameContent(ResolveAligned(target, source), _engine.Resolve(source, null)).ShouldBeTrue();
    }

    [Fact]
    public void AllRowsDifferent_IngredientsAreReplacedAndStepsPairedByPosition()
    {
        var target = new RecipeBuilder()
            .Ingredient(Flour, 200).Ingredient(Milk, 300)
            .Step(StepSection.Instructions, "A.").Step(StepSection.Instructions, "B.")
            .Recipe;
        var source = new RecipeBuilder()
            .Ingredient(Salt, 1).Ingredient(Eggs, 2).Ingredient(Salt, 2)
            .Step(StepSection.Instructions, "X.").Step(StepSection.Instructions, "Y.").Step(StepSection.Instructions, "Z.")
            .Recipe;

        var draft = Align(target, source);

        draft.Ingredients.ShouldAllBe(i => i.Id == null);
        var targetSteps = target.Steps.Select(s => (Guid?)s.Id).ToList();
        draft.InstructionSteps.Select(s => s.Id).ShouldBe([targetSteps[0], targetSteps[1], null]);
        ImportAlignment.HasSameContent(ResolveAligned(target, source), _engine.Resolve(source, null)).ShouldBeTrue();
    }

    [Fact]
    public void StepsBetweenAnchors_ArePairedByPosition_TheRestRemovedOrAdded()
    {
        var target = new RecipeBuilder()
            .Step(StepSection.Instructions, "Start.").Step(out var x, StepSection.Instructions, "X.").Step(out var y, StepSection.Instructions, "Y.").Step(StepSection.Instructions, "Koniec.")
            .Recipe;
        var source = new RecipeBuilder()
            .Step(StepSection.Instructions, "Start.").Step(StepSection.Instructions, "Z.").Step(StepSection.Instructions, "Koniec.").Step(StepSection.Instructions, "Podawaj.")
            .Recipe;

        var diff = _engine.Diff(target, Align(target, source));

        diff.StepOps.OfType<ReplaceStepOp>().Single().TargetId.ShouldBe(x);
        diff.StepOps.OfType<RemoveStepOp>().Single().TargetId.ShouldBe(y);
        diff.StepOps.OfType<AddStepOp>().Single().Blocks.ShouldBe([new TextBlock("Podawaj.")]);
    }

    [Fact]
    public void EmptySections_AddAndRemoveWholeSections()
    {
        var target = new RecipeBuilder().Ingredient(Flour, 200).Step(StepSection.Prep, "Odmierz.").Step(StepSection.Instructions, "Smaż.").Recipe;
        var source = new RecipeBuilder().Ingredient(Flour, 200).Step(StepSection.Instructions, "Smaż.").Step(StepSection.Instructions, "Podawaj.").Recipe;
        var empty = new RecipeBuilder().Ingredient(Flour, 200).Step(StepSection.Instructions, "Smaż.").Recipe;

        var diff = _engine.Diff(target, Align(target, source));

        diff.StepOps.OfType<RemoveStepOp>().Single().Section.ShouldBe(StepSection.Prep);
        diff.StepOps.OfType<AddStepOp>().Single().Section.ShouldBe(StepSection.Instructions);
        Align(empty, empty).PrepSteps.ShouldBeEmpty();
        _engine.Diff(empty, Align(empty, target)).StepOps.OfType<AddStepOp>().Single().Section.ShouldBe(StepSection.Prep);
    }

    [Fact]
    public void DuplicateProducts_AreMatchedInOrder()
    {
        var target = new RecipeBuilder().Ingredient(out var first, Salt, 1).Ingredient(Flour, 200).Ingredient(out var second, Salt, 2).Recipe;
        var source = new RecipeBuilder().Ingredient(Salt, 1).Ingredient(Salt, 3).Recipe;

        var draft = Align(target, source);

        draft.Ingredients.Select(i => i.Id).ShouldBe([first, second]);
        var diff = _engine.Diff(target, draft);
        diff.IngredientOps.OfType<RemoveIngredientOp>().Single().TargetId.ShouldBe(target.Ingredients[1].Id);
        diff.IngredientOps.OfType<ModifyIngredientOp>().Single().TargetId.ShouldBe(second);
    }

    [Fact]
    public void AddedRows_FollowTheNearestPrecedingMatchedRow()
    {
        var target = new RecipeBuilder().Ingredient(out var flour, Flour, 200).Ingredient(Milk, 300).Recipe;
        var source = new RecipeBuilder().Ingredient(Flour, 200).Ingredient(Eggs, 2).Ingredient(Salt, 1).Ingredient(Milk, 300).Recipe;

        var adds = _engine.Diff(target, Align(target, source)).IngredientOps.OfType<AddIngredientOp>().ToList();

        adds.Count.ShouldBe(2);
        adds[0].AfterId.ShouldBe(flour);
        adds[1].AfterId.ShouldBe(adds[0].Id);
    }

    [Fact]
    public void UtensilsAreASetDifference_AndAMissingTimeKeepsTheTarget()
    {
        var target = new RecipeBuilder().Ingredient(Flour, 200).Utensil(Pan).Times(10, 20).Recipe;
        var source = new RecipeBuilder().Ingredient(Flour, 200).Utensil(Oven).Times(null, 30).Recipe;

        var diff = _engine.Diff(target, Align(target, source));

        diff.UtensilIdsAdded.ShouldBe([Oven]);
        diff.UtensilIdsRemoved.ShouldBe([Pan]);
        diff.PrepMinutes.ShouldBeNull();
        diff.CookMinutes.ShouldBe(30);
    }

    [Theory]
    [InlineData("  Smaż\n  na   złoto ", "Smaż na złoto")]
    [InlineData("\t", "")]
    public void StepText_IsTrimmedWithWhitespaceCollapsed(string text, string expected) =>
        ImportAlignment.StepText([new TextBlock(text)]).ShouldBe(expected);

    [Fact]
    public void StepText_JoinsTextBlocksAndIgnoresPhotos() =>
        ImportAlignment.StepText([new TextBlock("Smaż"), new ImageBlock(Guid.CreateVersion7(), "zdjęcie"), new TextBlock(" na złoto.")])
            .ShouldBe("Smaż na złoto.");

    [Fact]
    public void HasSameContent_IgnoresIdsButNotOrder()
    {
        var a = new RecipeBuilder().Ingredient(Flour, 200).Ingredient(Milk, 300).Recipe;
        var b = new RecipeBuilder().Ingredient(Flour, 200).Ingredient(Milk, 300).Recipe;
        var swapped = new RecipeBuilder().Ingredient(Milk, 300).Ingredient(Flour, 200).Recipe;

        ImportAlignment.HasSameContent(_engine.Resolve(a, null), _engine.Resolve(b, null)).ShouldBeTrue();
        ImportAlignment.HasSameContent(_engine.Resolve(a, null), _engine.Resolve(swapped, null)).ShouldBeFalse();
    }

    private RecipeDraft Align(Recipe target, Recipe source) =>
        ImportAlignment.Align(_engine.Resolve(target, null), _engine.Resolve(source, null));

    private ResolvedRecipeModel ResolveAligned(Recipe target, Recipe source) =>
        _engine.Resolve(target, RecipeBuilder.VariantOf(target, _engine.Diff(target, Align(target, source))));
}
