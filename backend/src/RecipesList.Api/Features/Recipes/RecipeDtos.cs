using System.Text.Json.Serialization;
using RecipesList.Api.Features.Images;
using RecipesList.Api.Features.Units;
using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Visibility;

namespace RecipesList.Api.Features.Recipes;

/// <summary>A page of results (API.md §0 <c>Paged</c>).</summary>
internal sealed record PagedDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

/// <summary>A user as shown to others; never carries the email.</summary>
internal sealed record UserRefDto(Guid Id, string Nickname);

/// <summary>A tag; <see cref="IsSystem"/> is true for the computed <c>szybkie</c>.</summary>
internal sealed record TagRefDto(Guid Id, string Name, bool IsSystem);

/// <summary>A kitchen utensil.</summary>
internal sealed record UtensilRefDto(Guid Id, string Name);

/// <summary>A catalog product embedded in an ingredient (API.md §0 <c>ProductRef</c>).</summary>
internal sealed record RecipeProductRefDto(
    Guid Id,
    string Name,
    ProductCategory Category,
    MeasureKind MeasureKind,
    DisplayClass DisplayClass,
    bool HasSizes,
    bool AllowHalf,
    decimal? GramsPerMl,
    decimal? GramsPerPiece,
    decimal? DefaultPackageAmount,
    PluralForms? PieceForms,
    bool IsVerified)
{
    public static RecipeProductRefDto From(Product p) => new(
        p.Id, p.Name, p.Category, p.MeasureKind, p.DisplayClass, p.HasSizes, p.AllowHalf,
        p.GramsPerMl, p.GramsPerPiece, p.DefaultPackageAmount, p.PieceForms, p.IsVerified);
}

/// <summary>
/// An ingredient row of a recipe. <c>Quantity</c> is as the author entered it (null means "do smaku");
/// a custom unit is embedded in it; <c>BaseAmount</c> is the server-computed canonical amount (g, ml or medium pieces), null iff there is no quantity.
/// </summary>
internal sealed record IngredientDto(Guid Id, RecipeProductRefDto Product, QuantityDto? Quantity, decimal? BaseAmount, string? Note, bool Optional);

/// <summary>A prep or instruction step.</summary>
internal sealed record StepDto(Guid Id, IReadOnlyList<ContentBlock> Blocks);

/// <summary>A recipe card in lists (API.md §4 <c>RecipeSummary</c>). <c>VariantCount</c> counts variants visible to the caller.</summary>
internal sealed record RecipeSummaryDto(
    Guid Id,
    string Title,
    UserRefDto Author,
    ImageInfoDto? CoverImage,
    VisibilityLevel Visibility,
    bool IsFavorite,
    IReadOnlyList<TagRefDto> Tags,
    int? PrepMinutes,
    int? CookMinutes,
    int VariantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// A whole recipe (API.md §4 <c>RecipeDetail</c>). <c>Version</c> is sent back on <c>PUT</c> (optimistic concurrency);
/// <c>CanEdit</c> means the caller is the author and the recipe is neither public nor archived; <c>TotalVariantCount</c> and
/// <c>ForeignVariantCount</c> are 0 unless the caller is the author; <c>VariantCount</c> counts variants visible to the caller.
/// </summary>
internal sealed record RecipeDetailDto(
    Guid Id,
    uint Version,
    bool IsArchived,
    VisibilityLevel Visibility,
    bool IsFavorite,
    bool CanEdit,
    int TotalVariantCount,
    int ForeignVariantCount,
    string Title,
    string? Description,
    ImageInfoDto? CoverImage,
    UserRefDto Author,
    int? PrepMinutes,
    int? CookMinutes,
    int? Servings,
    IReadOnlyList<TagRefDto> Tags,
    IReadOnlyList<UtensilRefDto> Utensils,
    IReadOnlyList<IngredientDto> Ingredients,
    IReadOnlyList<StepDto> PrepSteps,
    IReadOnlyList<StepDto> InstructionSteps,
    int CookCount,
    DateTimeOffset? MyLastCountedCookAt,
    int VariantCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>An ingredient row in a write; omit <see cref="Id"/> for a new row.</summary>
internal sealed record IngredientWriteDto(Guid? Id, [property: JsonRequired] Guid ProductId, Quantity? Quantity, string? Note, bool Optional);

/// <summary>A step in a write; omit <see cref="Id"/> for a new step.</summary>
internal sealed record StepWriteDto(Guid? Id, [property: JsonRequired] List<ContentBlock> Blocks);

/// <summary>
/// Body of <c>POST /recipes</c> (<c>RecipeWrite &amp; { visibility }</c>) and <c>PUT /recipes/{id}</c>
/// (<c>RecipeWrite &amp; { version, deleteVariants }</c>). One shape serves both; the endpoint insists on the extras it needs.
/// </summary>
internal sealed record RecipeWriteRequest(
    [property: JsonRequired] string Title,
    string? Description,
    Guid? CoverImageId,
    int? PrepMinutes,
    int? CookMinutes,
    int? Servings,
    [property: JsonRequired] List<Guid> TagIds,
    [property: JsonRequired] List<Guid> UtensilIds,
    [property: JsonRequired] List<IngredientWriteDto> Ingredients,
    [property: JsonRequired] List<StepWriteDto> PrepSteps,
    [property: JsonRequired] List<StepWriteDto> InstructionSteps,
    VisibilityLevel? Visibility,
    uint? Version,
    bool? DeleteVariants)
{
    public RecipeWriteInput ToInput() => new(
        Title,
        Description,
        CoverImageId,
        PrepMinutes,
        CookMinutes,
        Servings,
        TagIds,
        UtensilIds,
        [.. Ingredients.Select(i => new IngredientWriteInput(i.Id, i.ProductId, i.Quantity, i.Note, i.Optional))],
        [.. PrepSteps.Select(s => new StepWriteInput(s.Id, s.Blocks))],
        [.. InstructionSteps.Select(s => new StepWriteInput(s.Id, s.Blocks))]);
}

/// <summary>Body of <c>PUT /recipes/{id}/visibility</c>.</summary>
internal sealed record SetVisibilityRequest([property: JsonRequired] VisibilityLevel Visibility);

/// <summary>Body of <c>POST /recipes/{id}/cooked</c>: the variant that was shown, if any.</summary>
internal sealed record CookRequest(Guid? VariantId);

/// <summary>A recorded cook (API.md §4 <c>CookResult</c>).</summary>
internal sealed record CookResultDto(
    bool Counted, int RecipeCookCount, [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? VariantCookCount, DateTimeOffset NextAllowedAt)
{
    public static CookResultDto From(CookResult r) => new(true, r.RecipeCookCount, r.VariantCookCount, r.NextAllowedAt);
}
