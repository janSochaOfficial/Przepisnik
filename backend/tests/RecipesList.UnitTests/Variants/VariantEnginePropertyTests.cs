using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;

namespace RecipesList.UnitTests.Variants;

/// <summary>
/// Property-style checks over seeded random recipes and edits: <c>Resolve(base, Diff(base, edited))</c> equals the
/// edited copy (up to the ids of added rows), and the matrix invariants hold for any set of such variants.
/// </summary>
public sealed class VariantEnginePropertyTests
{
    private const int Iterations = 400;
    private static readonly Guid[] Products = [.. Enumerable.Range(0, 6).Select(_ => Guid.CreateVersion7())];
    private static readonly Guid[] Utensils = [.. Enumerable.Range(0, 5).Select(_ => Guid.CreateVersion7())];
    private static readonly decimal?[] Amounts = [null, 1m, 100m, 100.5m, 101m, 150m, 2.6m];

    private readonly VariantEngine _engine = new();

    public static TheoryData<int> Seeds() => [1, 2, 3, 4, 5];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void ResolveOfDiff_RandomEdits_EqualsTheEditedCopy(int seed)
    {
        var random = new Random(seed);
        for (var n = 0; n < Iterations; n++)
        {
            var recipe = RandomRecipe(random);
            var edited = RandomEdit(random, recipe);

            var diff = _engine.Diff(recipe, edited);
            var resolved = _engine.Resolve(recipe, RecipeBuilder.VariantOf(recipe, diff));

            AssertEquivalent(edited, resolved, $"seed {seed}, iteration {n}");
            diff.IsEmpty.ShouldBe(IsUnchanged(recipe, edited), $"seed {seed}, iteration {n}");
        }
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void BuildMatrix_RandomVariants_EveryColumnInExactlyOneOptionPerSlot(int seed)
    {
        var random = new Random(seed);
        for (var n = 0; n < Iterations / 4; n++)
        {
            var recipe = RandomRecipe(random);
            var variants = Enumerable.Range(0, random.Next(0, 6))
                .Select(_ => RecipeBuilder.VariantOf(recipe, _engine.Diff(recipe, RandomEdit(random, recipe))))
                .ToList();

            var matrix = _engine.BuildMatrix(recipe, variants);

            var columns = matrix.Variants.Select(v => v.VariantId).ToList();
            columns.Count.ShouldBe(variants.Count + 1);
            matrix.Slots.Count(s => s.Key.StartsWith("i:", StringComparison.Ordinal)).ShouldBe(recipe.Ingredients.Count);
            matrix.Slots.Select(s => s.Key).ShouldBeUnique();
            foreach (var slot in matrix.Slots)
            {
                slot.Options.SelectMany(o => o.VariantIds).ShouldBe(columns, ignoreOrder: true);
                slot.Options.Select(o => o.Key).ShouldBeUnique();
                foreach (var option in slot.Options.Where(o => o.Ingredient is not null))
                {
                    option.Key.ShouldBe(MatrixKeys.Option(option.Ingredient!.ProductId, option.Ingredient.BaseAmount));
                }
            }
        }
    }

    private static Recipe RandomRecipe(Random random)
    {
        var builder = new RecipeBuilder();
        for (var i = random.Next(1, 7); i > 0; i--)
        {
            builder.Ingredient(Pick(random, Products), Pick(random, Amounts), random.Next(4) == 0 ? "drobno" : null, random.Next(5) == 0);
        }

        foreach (var section in new[] { StepSection.Prep, StepSection.Instructions })
        {
            for (var i = random.Next(0, 4); i > 0; i--)
            {
                builder.Step(section, $"Krok {random.Next(100)}");
            }
        }

        foreach (var utensil in Utensils.Where(_ => random.Next(3) == 0))
        {
            builder.Utensil(utensil);
        }

        return builder.Times(random.Next(3) == 0 ? null : random.Next(0, 40), random.Next(3) == 0 ? null : random.Next(0, 40)).Recipe;
    }

    private static RecipeDraft RandomEdit(Random random, Recipe recipe)
    {
        var draft = RecipeBuilder.DraftOf(recipe);

        var ingredients = new List<IngredientDraft>();
        foreach (var row in draft.Ingredients)
        {
            InsertNew(random, ingredients, () => RecipeBuilder.NewIngredient(Pick(random, Products), Pick(random, Amounts), random.Next(2) == 0 ? Guid.CreateVersion7() : null));
            switch (random.Next(5))
            {
                case 0:
                    break; // removed
                case 1:
                    var amount = Pick(random, Amounts);
                    ingredients.Add(row with
                    {
                        ProductId = Pick(random, Products),
                        Quantity = amount is { } a ? new Quantity(a, Unit.G) : null,
                        BaseAmount = amount,
                        Note = random.Next(2) == 0 ? null : "inaczej",
                        Optional = random.Next(2) == 0,
                    });
                    break;
                default:
                    ingredients.Add(row);
                    break;
            }
        }

        InsertNew(random, ingredients, () => RecipeBuilder.NewIngredient(Pick(random, Products), Pick(random, Amounts)));

        // Times change to a value or stay; clearing a base time is not representable (VariantEngine remarks).
        var prep = random.Next(3) == 0 ? random.Next(0, 60) : draft.PrepMinutes;
        var cook = random.Next(3) == 0 ? random.Next(0, 60) : draft.CookMinutes;

        return new RecipeDraft(
            ingredients,
            EditSteps(random, draft.PrepSteps),
            EditSteps(random, draft.InstructionSteps),
            [.. Utensils.Where(_ => random.Next(3) == 0)],
            prep,
            cook);
    }

    private static List<StepDraft> EditSteps(Random random, IReadOnlyList<StepDraft> steps)
    {
        var edited = new List<StepDraft>();
        foreach (var step in steps)
        {
            InsertNew(random, edited, () => new StepDraft(random.Next(2) == 0 ? Guid.CreateVersion7() : null, [new TextBlock($"Nowy {random.Next(100)}")]));
            switch (random.Next(4))
            {
                case 0:
                    break;
                case 1:
                    edited.Add(step with { Blocks = [new TextBlock("Zmieniony"), new ImageBlock(Guid.CreateVersion7(), null)] });
                    break;
                default:
                    edited.Add(step);
                    break;
            }
        }

        InsertNew(random, edited, () => new StepDraft(null, [new TextBlock("Na końcu")]));
        return edited;
    }

    private static void InsertNew<T>(Random random, List<T> list, Func<T> create)
    {
        while (random.Next(4) == 0)
        {
            list.Add(create());
        }
    }

    private static T Pick<T>(Random random, IReadOnlyList<T> items) => items[random.Next(items.Count)];

    private static void AssertEquivalent(RecipeDraft edited, ResolvedRecipeModel resolved, string context)
    {
        resolved.Ingredients.Count.ShouldBe(edited.Ingredients.Count, context);
        for (var i = 0; i < edited.Ingredients.Count; i++)
        {
            var e = edited.Ingredients[i];
            var r = resolved.Ingredients[i];
            if (e.Id is { } id)
            {
                r.Id.ShouldBe(id, context);
            }

            (r.ProductId, r.Quantity, r.BaseAmount, r.Note, r.Optional).ShouldBe((e.ProductId, e.Quantity, e.BaseAmount, e.Note, e.Optional), context);
        }

        AssertSteps(edited.PrepSteps, resolved.PrepSteps, context);
        AssertSteps(edited.InstructionSteps, resolved.InstructionSteps, context);
        resolved.UtensilIds.ShouldBe(edited.UtensilIds, ignoreOrder: true, context);
        (resolved.PrepMinutes, resolved.CookMinutes).ShouldBe((edited.PrepMinutes, edited.CookMinutes), context);
    }

    private static void AssertSteps(IReadOnlyList<StepDraft> edited, IReadOnlyList<ResolvedStep> resolved, string context)
    {
        resolved.Count.ShouldBe(edited.Count, context);
        for (var i = 0; i < edited.Count; i++)
        {
            if (edited[i].Id is { } id)
            {
                resolved[i].Id.ShouldBe(id, context);
            }

            resolved[i].Blocks.SequenceEqual(edited[i].Blocks).ShouldBeTrue(context);
        }
    }

    private static bool IsUnchanged(Recipe recipe, RecipeDraft edited)
    {
        var original = RecipeBuilder.DraftOf(recipe);
        return original.Ingredients.SequenceEqual(edited.Ingredients)
            && original.PrepSteps.Select(s => s.Id).SequenceEqual(edited.PrepSteps.Select(s => s.Id))
            && original.PrepSteps.Zip(edited.PrepSteps).All(p => p.First.Blocks.SequenceEqual(p.Second.Blocks))
            && original.InstructionSteps.Select(s => s.Id).SequenceEqual(edited.InstructionSteps.Select(s => s.Id))
            && original.InstructionSteps.Zip(edited.InstructionSteps).All(p => p.First.Blocks.SequenceEqual(p.Second.Blocks))
            && original.UtensilIds.Order().SequenceEqual(edited.UtensilIds.Order())
            && original.PrepMinutes == edited.PrepMinutes
            && original.CookMinutes == edited.CookMinutes;
    }
}
