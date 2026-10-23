using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Variants;

namespace RecipesList.UnitTests.Variants;

/// <summary>Builds base recipes and drafts for engine tests (TECHNICAL §6: no magic GUIDs inline).</summary>
internal sealed class RecipeBuilder
{
    private readonly Recipe _recipe = new() { Title = "Przepis", NormalizedTitle = "przepis" };

    public Recipe Recipe => _recipe;

    public static Guid NewProduct() => Guid.CreateVersion7();

    public RecipeBuilder Ingredient(Guid productId, decimal? grams, string? note = null, bool optional = false) =>
        Ingredient(out _, productId, grams, note, optional);

    public RecipeBuilder Ingredient(out Guid id, Guid productId, decimal? grams, string? note = null, bool optional = false)
    {
        var row = new RecipeIngredient
        {
            RecipeId = _recipe.Id,
            Position = _recipe.Ingredients.Count,
            ProductId = productId,
            Quantity = grams is { } g ? new Quantity(g, Unit.G) : null,
            BaseAmount = grams,
            Note = note,
            Optional = optional,
        };
        _recipe.Ingredients.Add(row);
        id = row.Id;
        return this;
    }

    public RecipeBuilder Step(StepSection section, string text) => Step(out _, section, text);

    public RecipeBuilder Step(out Guid id, StepSection section, string text)
    {
        var step = new RecipeStep
        {
            RecipeId = _recipe.Id,
            Section = section,
            Position = _recipe.Steps.Count(s => s.Section == section),
            Blocks = [new TextBlock(text)],
        };
        _recipe.Steps.Add(step);
        id = step.Id;
        return this;
    }

    public RecipeBuilder Utensil(Guid utensilId)
    {
        _recipe.Utensils.Add(new RecipeUtensil { RecipeId = _recipe.Id, UtensilId = utensilId });
        return this;
    }

    public RecipeBuilder Times(int? prep, int? cook)
    {
        _recipe.PrepMinutes = prep;
        _recipe.CookMinutes = cook;
        return this;
    }

    /// <summary>The unchanged edited copy of the recipe, as the variant editor would first send it.</summary>
    public static RecipeDraft DraftOf(Recipe recipe) => new(
        [.. recipe.Ingredients.OrderBy(i => i.Position)
            .Select(i => new IngredientDraft(i.Id, i.ProductId, i.Quantity, i.BaseAmount, i.Note, i.Optional))],
        [.. StepsOf(recipe, StepSection.Prep)],
        [.. StepsOf(recipe, StepSection.Instructions)],
        [.. recipe.Utensils.Select(u => u.UtensilId)],
        recipe.PrepMinutes,
        recipe.CookMinutes);

    public static IngredientDraft NewIngredient(Guid productId, decimal? grams, Guid? id = null) =>
        new(id, productId, grams is { } g ? new Quantity(g, Unit.G) : null, grams, null, false);

    public static Variant VariantOf(Recipe recipe, VariantDiff diff) => new()
    {
        RecipeId = recipe.Id,
        Name = "Wariant",
        IngredientOps = [.. diff.IngredientOps],
        StepOps = [.. diff.StepOps],
        UtensilIdsAdded = [.. diff.UtensilIdsAdded],
        UtensilIdsRemoved = [.. diff.UtensilIdsRemoved],
        PrepMinutes = diff.PrepMinutes,
        CookMinutes = diff.CookMinutes,
    };

    private static IEnumerable<StepDraft> StepsOf(Recipe recipe, StepSection section) =>
        recipe.Steps.Where(s => s.Section == section).OrderBy(s => s.Position).Select(s => new StepDraft(s.Id, s.Blocks));
}
