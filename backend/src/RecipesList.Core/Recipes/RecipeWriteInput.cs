using RecipesList.Core.Quantities;

namespace RecipesList.Core.Recipes;

/// <summary>An ingredient row of a recipe write. <see cref="Id"/> keeps an existing row (API.md §4 id stability).</summary>
public sealed record IngredientWriteInput(Guid? Id, Guid ProductId, Quantity? Quantity, string? Note, bool Optional);

/// <summary>A step of a recipe write; <see cref="Id"/> keeps an existing step.</summary>
public sealed record StepWriteInput(Guid? Id, IReadOnlyList<ContentBlock> Blocks);

/// <summary>The body of a recipe create or update (API.md §4 <c>RecipeWrite</c>).</summary>
public sealed record RecipeWriteInput(
    string Title,
    string? Description,
    Guid? CoverImageId,
    int? PrepMinutes,
    int? CookMinutes,
    IReadOnlyList<Guid> TagIds,
    IReadOnlyList<Guid> UtensilIds,
    IReadOnlyList<IngredientWriteInput> Ingredients,
    IReadOnlyList<StepWriteInput> PrepSteps,
    IReadOnlyList<StepWriteInput> InstructionSteps);
