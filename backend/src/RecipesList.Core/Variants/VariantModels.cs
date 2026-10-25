using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Units;
using RecipesList.Core.Visibility;

namespace RecipesList.Core.Variants;

/// <summary>An ingredient row of a submitted edited copy, before the server computes its base amount.</summary>
public sealed record IngredientInput(Guid? Id, Guid ProductId, Quantity? Quantity, string? Note, bool Optional);

/// <summary>The edited copy of the base recipe (API.md §5 <c>VariantWrite.recipe</c>).</summary>
public sealed record VariantRecipeInput(
    IReadOnlyList<IngredientInput> Ingredients,
    IReadOnlyList<StepDraft> PrepSteps,
    IReadOnlyList<StepDraft> InstructionSteps,
    IReadOnlyList<Guid> UtensilIds,
    int? PrepMinutes,
    int? CookMinutes,
    int? Servings);

/// <summary>A create or update of a variant (API.md §5 <c>VariantWrite</c>).</summary>
public sealed record VariantInput(string Name, string? Description, VisibilityLevel Visibility, VariantRecipeInput Recipe);

/// <summary>A variant as listed (API.md §5 <c>VariantSummary</c>).</summary>
public sealed record VariantSummaryModel(
    Guid Id,
    Guid RecipeId,
    string RecipeTitle,
    string Name,
    string? Description,
    Guid AuthorId,
    string AuthorNickname,
    VisibilityLevel Visibility,
    bool IsFavorite,
    bool BaseAvailable,
    int CookCount,
    int ChangeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>A variant with its operations (API.md §5 <c>VariantDetail</c>).</summary>
public sealed record VariantDetailModel(VariantSummaryModel Summary, Variant Variant)
{
    /// <summary>The custom units used by the variant's operations, by id (PROJECT §5.6).</summary>
    public IReadOnlyDictionary<Guid, CustomUnit> Units { get; init; } = new Dictionary<Guid, CustomUnit>();
}

/// <summary>A variant applied to its base, with everything needed to render <c>ResolvedRecipe</c>.</summary>
public sealed record ResolvedVariantModel(
    Recipe Recipe,
    Variant Variant,
    ResolvedRecipeModel Resolved,
    bool RecipeIsFavorite,
    int RecipeCookCount,
    string AuthorNickname,
    IReadOnlyList<Tag> Tags,
    IReadOnlyList<Utensil> Utensils,
    IReadOnlyDictionary<Guid, Product> Products)
{
    /// <summary>The custom units used by the resolved ingredients, by id (PROJECT §5.6).</summary>
    public IReadOnlyDictionary<Guid, CustomUnit> Units { get; init; } = new Dictionary<Guid, CustomUnit>();
}

/// <summary>A column of the matrix with the data the API shows for it.</summary>
public sealed record MatrixColumnModel(Guid? VariantId, string Name, Guid AuthorId, string AuthorNickname, int CookCount, DateTimeOffset CreatedAt, bool IsFavorite);

/// <summary>The matrix of one recipe for the caller, plus the column metadata and the referenced products.</summary>
public sealed record VariantMatrixResult(
    VariantMatrixModel Matrix,
    IReadOnlyList<MatrixColumnModel> Columns,
    IReadOnlyDictionary<Guid, Product> Products)
{
    /// <summary>The custom units used by the option ingredients, by id (PROJECT §5.6).</summary>
    public IReadOnlyDictionary<Guid, CustomUnit> Units { get; init; } = new Dictionary<Guid, CustomUnit>();
}

/// <summary>A selection to store (API.md §5.2 <c>SelectionWrite</c>).</summary>
public sealed record SelectionInput(IReadOnlyDictionary<string, SelectionPick> Picks, ScalingState? Scaling);

/// <summary>A page of results.</summary>
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);
