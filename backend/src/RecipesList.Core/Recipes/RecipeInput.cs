using RecipesList.Core.Quantities;

namespace RecipesList.Core.Recipes;

/// <summary>An ingredient row of a recipe write. <see cref="Id"/> keeps an existing row (API.md §4 id stability).</summary>
public sealed record IngredientInput(Guid? Id, Guid ProductId, Quantity? Quantity, string? Note, bool Optional);

/// <summary>A step of a recipe write; <see cref="Id"/> keeps an existing step.</summary>
public sealed record StepInput(Guid? Id, IReadOnlyList<ContentBlock> Blocks);

/// <summary>The body of a recipe create or update (API.md §4 <c>RecipeWrite</c>).</summary>
public sealed record RecipeInput(
    string Title,
    string? Description,
    Guid? CoverImageId,
    int? PrepMinutes,
    int? CookMinutes,
    IReadOnlyList<Guid> TagIds,
    IReadOnlyList<Guid> UtensilIds,
    IReadOnlyList<IngredientInput> Ingredients,
    IReadOnlyList<StepInput> PrepSteps,
    IReadOnlyList<StepInput> InstructionSteps);

/// <summary>The computed system tag <c>szybkie</c> (PROJECT §4.3, D-09).</summary>
public static class QuickTag
{
    /// <summary>Largest total of the set times, in minutes, that still counts as quick.</summary>
    public const int MaxMinutes = 30;

    /// <summary>At least one time is set and the sum of the set times is at most 30 minutes.</summary>
    public static bool Applies(int? prepMinutes, int? cookMinutes) =>
        (prepMinutes is not null || cookMinutes is not null)
        && (prepMinutes ?? 0) + (cookMinutes ?? 0) <= MaxMinutes;
}
