using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;

namespace RecipesList.UnitTests.Variants;

public sealed class VariantEngineTests
{
    private static readonly Guid Onion = RecipeBuilder.NewProduct();
    private static readonly Guid Chicken = RecipeBuilder.NewProduct();
    private static readonly Guid Pepper = RecipeBuilder.NewProduct();
    private static readonly Guid Garlic = RecipeBuilder.NewProduct();

    private readonly VariantEngine _engine = new();

    [Fact]
    public void Diff_UnchangedCopy_IsEmpty()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Step(StepSection.Instructions, "Smaż.").Utensil(Guid.NewGuid()).Times(10, 20).Recipe;

        _engine.Diff(recipe, RecipeBuilder.DraftOf(recipe)).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Diff_BlankNoteVersusNull_IsNotAChange()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Recipe;
        var draft = RecipeBuilder.DraftOf(recipe);
        draft = draft with { Ingredients = [draft.Ingredients[0] with { Note = "  " }] };

        _engine.Diff(recipe, draft).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Diff_RowWithoutId_GetsAnIdFromTheGenerator()
    {
        var generated = Guid.CreateVersion7();
        var recipe = new RecipeBuilder().Ingredient(out var onion, Onion, 100).Recipe;
        var draft = RecipeBuilder.DraftOf(recipe);
        draft = draft with { Ingredients = [.. draft.Ingredients, RecipeBuilder.NewIngredient(Pepper, 50)] };

        var diff = new VariantEngine(() => generated).Diff(recipe, draft);

        diff.IngredientOps.ShouldHaveSingleItem().ShouldBe(new AddIngredientOp(generated, onion, Pepper, new Quantity(50, Unit.G), 50, null, false));
    }

    [Fact]
    public void Diff_StepIdsNotInTheBase_AreNewStepsKeepingTheirIds()
    {
        // F2's draftFromMatch keeps the step ids of the best variant's resolved recipe, including ids of added steps.
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Step(out var first, StepSection.Instructions, "Smaż.").Recipe;
        var otherVariantStep = Guid.CreateVersion7();
        var draft = RecipeBuilder.DraftOf(recipe);
        draft = draft with
        {
            InstructionSteps = [new StepDraft(otherVariantStep, [new TextBlock("Rozgrzej patelnię.")]), .. draft.InstructionSteps],
            PrepSteps = [new StepDraft(Guid.CreateVersion7(), [new TextBlock("Pokrój.")])],
        };

        var diff = _engine.Diff(recipe, draft);

        diff.StepOps.Count.ShouldBe(2);
        diff.StepOps.OfType<AddStepOp>().ShouldContain(op => op.Section == StepSection.Instructions && op.Id == otherVariantStep && op.AfterId == null);
        diff.StepOps.OfType<AddStepOp>().ShouldContain(op => op.Section == StepSection.Prep && op.AfterId == null);
        var resolved = _engine.Resolve(recipe, RecipeBuilder.VariantOf(recipe, diff));
        resolved.InstructionSteps.Select(s => s.Id).ShouldBe([otherVariantStep, first]);
    }

    [Fact]
    public void Diff_IngredientIdNotInTheBase_IsAnAddKeepingItsId()
    {
        var recipe = new RecipeBuilder().Ingredient(out var onion, Onion, 100).Recipe;
        var foreignId = Guid.CreateVersion7();
        var draft = RecipeBuilder.DraftOf(recipe);
        draft = draft with { Ingredients = [.. draft.Ingredients, RecipeBuilder.NewIngredient(Pepper, 50, foreignId)] };

        var op = _engine.Diff(recipe, draft).IngredientOps.ShouldHaveSingleItem().ShouldBeOfType<AddIngredientOp>();

        op.Id.ShouldBe(foreignId);
        op.AfterId.ShouldBe(onion);
    }

    [Fact]
    public void Diff_SameNewIdTwice_MintsADistinctIdForTheSecondRow()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Recipe;
        var id = Guid.CreateVersion7();
        var draft = RecipeBuilder.DraftOf(recipe);
        draft = draft with { Ingredients = [.. draft.Ingredients, RecipeBuilder.NewIngredient(Pepper, 50, id), RecipeBuilder.NewIngredient(Garlic, 5, id)] };

        var adds = _engine.Diff(recipe, draft).IngredientOps.OfType<AddIngredientOp>().ToList();

        adds[0].Id.ShouldBe(id);
        adds[1].Id.ShouldNotBe(id);
        adds[1].AfterId.ShouldBe(id);
    }

    [Fact]
    public void Diff_ReorderedBaseRows_IsRejected()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Ingredient(Chicken, 500).Recipe;
        var draft = RecipeBuilder.DraftOf(recipe);
        draft = draft with { Ingredients = [draft.Ingredients[1], draft.Ingredients[0]] };

        var error = Should.Throw<DomainValidationException>(() => _engine.Diff(recipe, draft));

        error.Code.ShouldBe(ErrorCodes.ValidationFailed);
        error.Errors.Keys.ShouldBe(["recipe.ingredients[1].id"]);
    }

    [Fact]
    public void Diff_BaseRowSentTwice_IsRejected()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Recipe;
        var draft = RecipeBuilder.DraftOf(recipe);
        draft = draft with { Ingredients = [draft.Ingredients[0], draft.Ingredients[0]] };

        Should.Throw<DomainValidationException>(() => _engine.Diff(recipe, draft)).Errors.Keys.ShouldBe(["recipe.ingredients[1].id"]);
    }

    [Fact]
    public void Diff_BaseStepMovedToTheOtherSection_IsRejected()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Step(StepSection.Prep, "Pokrój.").Step(StepSection.Instructions, "Smaż.").Recipe;
        var draft = RecipeBuilder.DraftOf(recipe);
        draft = draft with { PrepSteps = [], InstructionSteps = [.. draft.PrepSteps, .. draft.InstructionSteps] };

        Should.Throw<DomainValidationException>(() => _engine.Diff(recipe, draft)).Errors.Keys.ShouldBe(["recipe.instructionSteps[0].id"]);
    }

    [Fact]
    public void Diff_ClearingABaseTime_IsRejected()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Times(10, 20).Recipe;
        var draft = RecipeBuilder.DraftOf(recipe) with { CookMinutes = null };

        Should.Throw<DomainValidationException>(() => _engine.Diff(recipe, draft)).Errors.Keys.ShouldBe(["recipe.cookMinutes"]);
    }

    [Fact]
    public void Diff_OpsOrder_IsModifyAndRemoveInBaseOrderThenAddsInEditedOrder()
    {
        var recipe = new RecipeBuilder().Ingredient(out var a, Onion, 100).Ingredient(out var b, Chicken, 500).Ingredient(out var c, Garlic, 5).Recipe;
        var draft = RecipeBuilder.DraftOf(recipe);
        var pepper = Guid.CreateVersion7();
        draft = draft with
        {
            Ingredients = [RecipeBuilder.NewIngredient(Pepper, 50, pepper), draft.Ingredients[1], draft.Ingredients[2] with { BaseAmount = 10, Quantity = new Quantity(10, Unit.G) }],
        };

        var ops = _engine.Diff(recipe, draft).IngredientOps;

        ops.Select(o => o.GetType().Name).ShouldBe([nameof(RemoveIngredientOp), nameof(ModifyIngredientOp), nameof(AddIngredientOp)]);
        ((RemoveIngredientOp)ops[0]).TargetId.ShouldBe(a);
        ((ModifyIngredientOp)ops[1]).TargetId.ShouldBe(c);
        ((AddIngredientOp)ops[2]).AfterId.ShouldBeNull();
        b.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void Diff_Utensils_AddedInEditedOrderAndRemovedInBaseOrder()
    {
        Guid u1 = Guid.CreateVersion7(), u2 = Guid.CreateVersion7(), u3 = Guid.CreateVersion7(), u4 = Guid.CreateVersion7();
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Utensil(u1).Utensil(u2).Recipe;
        var draft = RecipeBuilder.DraftOf(recipe) with { UtensilIds = [u4, u2, u3, u4] };

        var diff = _engine.Diff(recipe, draft);

        diff.UtensilIdsAdded.ShouldBe([u4, u3]);
        diff.UtensilIdsRemoved.ShouldBe([u1]);
    }

    [Fact]
    public void Resolve_ModifyOfMissingTarget_ThrowsNamingTheOp()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Recipe;
        var variant = new Variant { RecipeId = recipe.Id, Name = "Zepsuty", IngredientOps = [new ModifyIngredientOp(Guid.CreateVersion7(), Pepper, null, null, null, false)] };

        Should.Throw<InvalidOperationException>(() => _engine.Resolve(recipe, variant)).Message.ShouldContain("ingredient op #0 (modify)");
    }

    [Fact]
    public void Resolve_RemoveOfAnAddedRow_Throws()
    {
        var recipe = new RecipeBuilder().Ingredient(out var onion, Onion, 100).Recipe;
        var added = Guid.CreateVersion7();
        var variant = new Variant
        {
            RecipeId = recipe.Id,
            Name = "Zepsuty",
            IngredientOps = [new AddIngredientOp(added, onion, Pepper, null, null, null, false), new RemoveIngredientOp(added)],
        };

        Should.Throw<InvalidOperationException>(() => _engine.Resolve(recipe, variant)).Message.ShouldContain("ingredient op #1 (remove)");
    }

    [Fact]
    public void Resolve_AddAfterMissingRow_Throws()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Recipe;
        var variant = new Variant
        {
            RecipeId = recipe.Id,
            Name = "Zepsuty",
            IngredientOps = [new AddIngredientOp(Guid.CreateVersion7(), Guid.CreateVersion7(), Pepper, null, null, null, false)],
        };

        Should.Throw<InvalidOperationException>(() => _engine.Resolve(recipe, variant)).Message.ShouldContain("ingredient op #0 (add)");
    }

    [Fact]
    public void Resolve_StepOpOfMissingTarget_Throws()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Step(StepSection.Instructions, "Smaż.").Recipe;
        var variant = new Variant { RecipeId = recipe.Id, Name = "Zepsuty", StepOps = [new RemoveStepOp(StepSection.Instructions, Guid.CreateVersion7())] };

        Should.Throw<InvalidOperationException>(() => _engine.Resolve(recipe, variant)).Message.ShouldContain("step op #0 (remove)");
    }

    [Fact]
    public void Resolve_StepOpInTheWrongSection_Throws()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Step(out var prep, StepSection.Prep, "Pokrój.").Recipe;
        var variant = new Variant { RecipeId = recipe.Id, Name = "Zepsuty", StepOps = [new ReplaceStepOp(StepSection.Instructions, prep, [new TextBlock("x")])] };

        Should.Throw<InvalidOperationException>(() => _engine.Resolve(recipe, variant));
    }

    [Fact]
    public void Resolve_UtensilSetOps_AreIdempotent()
    {
        Guid u1 = Guid.CreateVersion7(), u2 = Guid.CreateVersion7();
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Utensil(u1).Recipe;
        var variant = new Variant { RecipeId = recipe.Id, Name = "W", UtensilIdsAdded = [u1, u2], UtensilIdsRemoved = [Guid.CreateVersion7()] };

        _engine.Resolve(recipe, variant).UtensilIds.ShouldBe([u1, u2]);
    }

    [Theory]
    [InlineData(10, 20, true)]
    [InlineData(null, 30, true)]
    [InlineData(0, null, true)]
    [InlineData(15, 16, false)]
    [InlineData(null, null, false)]
    public void QuickTagRule_Applies_AtMostThirtyMinutesWithATimeSet(int? prep, int? cook, bool quick) =>
        QuickTagRule.Applies(prep, cook).ShouldBe(quick);

    [Fact]
    public void Resolve_VariantTimes_OverrideTheBaseAndDecideTheQuickTag()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Times(10, 60).Recipe;
        var variant = new Variant { RecipeId = recipe.Id, Name = "Szybki", CookMinutes = 15 };

        var resolved = _engine.Resolve(recipe, variant);

        (resolved.PrepMinutes, resolved.CookMinutes, resolved.IsQuick).ShouldBe((10, 15, true));
        _engine.Resolve(recipe, null).IsQuick.ShouldBeFalse();
    }

    [Fact]
    public void BuildMatrix_AddedSlots_CarryTheBaseSlotTheyFollow()
    {
        var recipe = new RecipeBuilder().Ingredient(out var onion, Onion, 100).Ingredient(out var chicken, Chicken, 500).Recipe;
        Guid pepper = Guid.CreateVersion7(), garlic = Guid.CreateVersion7(), salt = Guid.CreateVersion7();
        var saltProduct = RecipeBuilder.NewProduct();
        var first = new Variant
        {
            RecipeId = recipe.Id,
            Name = "Pierwszy",
            IngredientOps =
            [
                new AddIngredientOp(pepper, onion, Pepper, null, null, null, false),
                new AddIngredientOp(garlic, pepper, Garlic, null, null, null, false),
                new AddIngredientOp(salt, null, saltProduct, null, null, null, false),
            ],
        };
        // A later variant adds pepper elsewhere; the first-seen position wins.
        var second = new Variant { RecipeId = recipe.Id, Name = "Drugi", IngredientOps = [new AddIngredientOp(Guid.CreateVersion7(), chicken, Pepper, null, null, null, false)] };

        var slots = _engine.BuildMatrix(recipe, [first, second]).Slots.ToDictionary(s => s.Key);

        slots[MatrixKeys.BaseSlot(onion)].AfterSlotKey.ShouldBeNull();
        slots[MatrixKeys.AddedSlot(Pepper)].AfterSlotKey.ShouldBe(MatrixKeys.BaseSlot(onion));
        slots[MatrixKeys.AddedSlot(Garlic)].AfterSlotKey.ShouldBe(MatrixKeys.BaseSlot(onion));
        slots[MatrixKeys.AddedSlot(saltProduct)].AfterSlotKey.ShouldBeNull();
    }

    [Fact]
    public void BuildMatrix_VariantAddingTheSameProductTwice_UsesItsFirstRowInResolvedOrder()
    {
        var recipe = new RecipeBuilder().Ingredient(out var onion, Onion, 100).Recipe;
        var variant = new Variant
        {
            RecipeId = recipe.Id,
            Name = "Dwa razy",
            IngredientOps =
            [
                new AddIngredientOp(Guid.CreateVersion7(), onion, Pepper, new Quantity(1, Unit.Piece), 1, null, false),
                new AddIngredientOp(Guid.CreateVersion7(), onion, Pepper, new Quantity(2, Unit.Piece), 2, null, false),
            ],
        };

        // Both rows follow the onion, so the second add ends up first in the resolved recipe.
        var slot = _engine.BuildMatrix(recipe, [variant]).Slots.Single(s => s.Key == MatrixKeys.AddedSlot(Pepper));

        slot.Options.Select(o => o.Key).ShouldBe([MatrixKeys.None, MatrixKeys.Option(Pepper, 2)]);
        slot.Options.SelectMany(o => o.VariantIds).Count().ShouldBe(2);
    }

    [Fact]
    public void BuildMatrix_NoVariants_HasOnlyTheBaseColumn()
    {
        var recipe = new RecipeBuilder().Ingredient(Onion, 100).Recipe;

        var matrix = _engine.BuildMatrix(recipe, []);

        matrix.Variants.ShouldHaveSingleItem().VariantId.ShouldBeNull();
        matrix.Slots.ShouldHaveSingleItem().Options.ShouldHaveSingleItem().VariantIds.ShouldBe([null]);
    }

    [Theory]
    [InlineData("200", "202", true)]
    [InlineData("200", "198", true)]
    [InlineData("200", "202.0001", false)]
    [InlineData("200", "197.9999", false)]
    [InlineData(null, null, true)]
    [InlineData("200", null, false)]
    public void IsSameAmount_OnePercentOfTheOptionAmount(string? option, string? amount, bool same) =>
        VariantEngine.IsSameAmount(Dec(option), Dec(amount)).ShouldBe(same);

    [Theory]
    [InlineData("200", "200")]
    [InlineData("200.000", "200")]
    [InlineData("27.3", "27.3")]
    [InlineData("108.6957", "108.696")]
    [InlineData("0.0005", "0.001")]
    [InlineData("2.5", "2.5")]
    [InlineData(null, "x")]
    public void MatrixKeys_Option_RoundsToThreeDecimalsWithoutTrailingZeros(string? amount, string suffix)
    {
        var product = Guid.Parse("00000000-0000-7000-8000-00000000a001");

        MatrixKeys.Option(product, Dec(amount)).ShouldBe($"{product}:{suffix}");
        MatrixKeys.OptionProductId(MatrixKeys.Option(product, Dec(amount))).ShouldBe(product);
    }

    [Theory]
    [InlineData("i:00000000-0000-7000-8000-00000000b001", true)]
    [InlineData("p:00000000-0000-7000-8000-00000000a001", true)]
    [InlineData("x:00000000-0000-7000-8000-00000000a001", false)]
    [InlineData("i:nope", false)]
    [InlineData("none", false)]
    public void MatrixKeys_IsWellFormedSlot(string key, bool expected) => MatrixKeys.IsWellFormedSlot(key).ShouldBe(expected);

    private static decimal? Dec(string? s) => s is null ? null : decimal.Parse(s, System.Globalization.CultureInfo.InvariantCulture);
}
