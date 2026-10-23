using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Variants;

/// <summary>Diff, resolve and matrix logic for variants (PROJECT §6, API.md §5). Implemented in B4.</summary>
public interface IVariantEngine
{
    /// <summary>Computes the operations that turn <paramref name="baseRecipe"/> into <paramref name="edited"/> (D-12).</summary>
    VariantDiff Diff(Recipe baseRecipe, RecipeDraft edited);

    /// <summary>Applies a variant's operations to its base; a null variant resolves to the base itself.</summary>
    ResolvedRecipeModel Resolve(Recipe baseRecipe, Variant? variant);

    /// <summary>Builds the slot/option matrix used by the client-side auto-match (API.md §5.1).</summary>
    VariantMatrixModel BuildMatrix(Recipe baseRecipe, IReadOnlyList<Variant> variants);
}

/// <summary>An ingredient row of an edited recipe; <see cref="Id"/> is set for rows kept from the base.</summary>
public sealed record IngredientDraft(Guid? Id, Guid ProductId, Quantity? Quantity, decimal? BaseAmount, string? Note, bool Optional);

/// <summary>A step of an edited recipe; <see cref="Id"/> is set for steps kept from the base.</summary>
public sealed record StepDraft(Guid? Id, IReadOnlyList<ContentBlock> Blocks);

/// <summary>The full edited copy the variant editor sends (API.md §5 <c>VariantWrite.recipe</c>).</summary>
public sealed record RecipeDraft(
    IReadOnlyList<IngredientDraft> Ingredients,
    IReadOnlyList<StepDraft> PrepSteps,
    IReadOnlyList<StepDraft> InstructionSteps,
    IReadOnlyList<Guid> UtensilIds,
    int? PrepMinutes,
    int? CookMinutes);

/// <summary>The operations of a variant, as computed by <see cref="IVariantEngine.Diff"/>.</summary>
public sealed record VariantDiff(
    IReadOnlyList<IngredientOp> IngredientOps,
    IReadOnlyList<StepOp> StepOps,
    IReadOnlyList<Guid> UtensilIdsAdded,
    IReadOnlyList<Guid> UtensilIdsRemoved,
    int? PrepMinutes,
    int? CookMinutes)
{
    /// <summary>True when the edit changes nothing (→ 400 <c>variant.no_changes</c>).</summary>
    public bool IsEmpty =>
        IngredientOps.Count == 0 && StepOps.Count == 0 && UtensilIdsAdded.Count == 0 && UtensilIdsRemoved.Count == 0
        && PrepMinutes is null && CookMinutes is null;
}

/// <summary>An ingredient of a resolved recipe. Base rows keep their id; added rows use the op id.</summary>
public sealed record ResolvedIngredient(Guid Id, Guid ProductId, Quantity? Quantity, decimal? BaseAmount, string? Note, bool Optional);

/// <summary>A step of a resolved recipe.</summary>
public sealed record ResolvedStep(Guid Id, IReadOnlyList<ContentBlock> Blocks);

/// <summary>A base recipe with a variant's operations applied (or the base itself).</summary>
public sealed record ResolvedRecipeModel(
    Guid RecipeId,
    Guid? VariantId,
    IReadOnlyList<ResolvedIngredient> Ingredients,
    IReadOnlyList<ResolvedStep> PrepSteps,
    IReadOnlyList<ResolvedStep> InstructionSteps,
    IReadOnlyList<Guid> UtensilIds,
    int? PrepMinutes,
    int? CookMinutes)
{
    /// <summary>Whether the effective times earn the system tag <c>szybkie</c> (D-09).</summary>
    public bool IsQuick => QuickTagRule.Applies(PrepMinutes, CookMinutes);
}

/// <summary>A column of the matrix: the base (<see cref="VariantId"/> null, API id <c>"base"</c>) or a variant.</summary>
public sealed record MatrixVariantModel(Guid? VariantId, IReadOnlyList<ProductSwap> ProductSwaps);

/// <summary>A product replaced by a variant, used for preference scoring.</summary>
public sealed record ProductSwap(Guid FromProductId, Guid ToProductId);

/// <summary>One option of a slot. <see cref="Ingredient"/> is null for <c>"none"</c>; null variant ids mean the base.</summary>
public sealed record SlotOptionModel(string Key, ResolvedIngredient? Ingredient, IReadOnlyList<Guid?> VariantIds);

/// <summary>A slot: a base ingredient (<c>i:&lt;id&gt;</c>) or a product added by variants (<c>p:&lt;productId&gt;</c>).</summary>
public sealed record SlotModel(string Key, Guid ProductId, IReadOnlyList<SlotOptionModel> Options)
{
    /// <summary>
    /// For an added slot: the key of the base slot its row follows in the first variant that adds it (the nearest
    /// base row before it), or null when no base row precedes it. Always null for base slots.
    /// </summary>
    public string? AfterSlotKey { get; init; }
}

/// <summary>The matrix for one recipe and the variants visible to the caller.</summary>
public sealed record VariantMatrixModel(Guid RecipeId, IReadOnlyList<MatrixVariantModel> Variants, IReadOnlyList<SlotModel> Slots);
