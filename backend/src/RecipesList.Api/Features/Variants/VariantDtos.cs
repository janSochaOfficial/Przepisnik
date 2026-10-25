using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using RecipesList.Api.Features.Units;
using RecipesList.Core.Catalog;
using RecipesList.Core.Images;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;
using RecipesList.Core.Units;
using RecipesList.Core.Variants;
using RecipesList.Core.Visibility;

namespace RecipesList.Api.Features.Variants;

// Request and response shapes of API.md §5. Shared shapes (ProductRef, UserRef, …) are declared here under
// variant-specific names until a common DTO module exists, so they cannot clash with other features' types.

/// <summary>
/// A quantity as the author entered it, in a write (API.md §0 <c>Quantity</c>). The read-only <c>customUnit</c> is not
/// a member, so a client that sends it back has it ignored. Responses use <see cref="QuantityDto"/>.
/// </summary>
public sealed record VariantQuantityDto(decimal Value, Unit Unit, PieceSize? Size, Guid? CustomUnitId = null)
{
    public Quantity ToQuantity() => new(Value, Unit, Size, CustomUnitId);
}

/// <summary>An ingredient row of the edited copy (API.md §4 <c>IngredientWrite</c>).</summary>
public sealed record VariantIngredientWrite
{
    /// <summary>The base row id for kept rows; null (or any non-base id) for new rows.</summary>
    public Guid? Id { get; init; }

    public required Guid ProductId { get; init; }

    /// <summary>Null means "do smaku".</summary>
    public VariantQuantityDto? Quantity { get; init; }

    [MaxLength(300)]
    public string? Note { get; init; }

    public required bool Optional { get; init; }
}

/// <summary>A step of the edited copy (API.md §4 <c>StepWrite</c>).</summary>
public sealed record VariantStepWrite
{
    public Guid? Id { get; init; }

    [Required, MinLength(1)]
    public required IReadOnlyList<ContentBlock> Blocks { get; init; }
}

/// <summary>The edited copy of the base recipe (API.md §5 <c>VariantWrite.recipe</c>).</summary>
public sealed record VariantRecipeWrite
{
    [Range(0, 2880)]
    public int? PrepMinutes { get; init; }

    [Range(0, 2880)]
    public int? CookMinutes { get; init; }

    /// <summary>The servings of the edited copy (PROJECT §5.7); null or the base's value inherits.</summary>
    [Range(1, 100)]
    public int? Servings { get; init; }

    [Required]
    public required IReadOnlyList<Guid> UtensilIds { get; init; }

    [Required, MinLength(1), MaxLength(100)]
    public required IReadOnlyList<VariantIngredientWrite> Ingredients { get; init; }

    [Required, MaxLength(50)]
    public required IReadOnlyList<VariantStepWrite> PrepSteps { get; init; }

    [Required, MinLength(1), MaxLength(50)]
    public required IReadOnlyList<VariantStepWrite> InstructionSteps { get; init; }

    public VariantRecipeInput ToInput() => new(
        [.. Ingredients.Select(i => new IngredientInput(i.Id, i.ProductId, i.Quantity?.ToQuantity(), i.Note, i.Optional))],
        [.. PrepSteps.Select(s => new StepDraft(s.Id, s.Blocks))],
        [.. InstructionSteps.Select(s => new StepDraft(s.Id, s.Blocks))],
        UtensilIds,
        PrepMinutes,
        CookMinutes,
        Servings);
}

/// <summary>Creates a variant (API.md §5 <c>VariantWrite</c>).</summary>
public sealed record VariantCreateRequest
{
    /// <summary>1–80 characters, unique per recipe per author.</summary>
    [Required, StringLength(80, MinimumLength = 1)]
    public required string Name { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    /// <summary>Effective audience = recipe audience ∩ variant audience (PROJECT §6.1).</summary>
    public required VisibilityLevel Visibility { get; init; }

    [Required]
    public required VariantRecipeWrite Recipe { get; init; }

    public VariantInput ToInput() => new(Name, Description, Visibility, Recipe.ToInput());
}

/// <summary>Edits a variant (API.md §5 <c>VariantWrite &amp; { version }</c>).</summary>
public sealed record VariantUpdateRequest
{
    [Required, StringLength(80, MinimumLength = 1)]
    public required string Name { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }

    public required VisibilityLevel Visibility { get; init; }

    [Required]
    public required VariantRecipeWrite Recipe { get; init; }

    /// <summary>The version last read; a mismatch → 409 <c>concurrency.conflict</c>.</summary>
    public required uint Version { get; init; }

    public VariantInput ToInput() => new(Name, Description, Visibility, Recipe.ToInput());
}

/// <summary>A user as shown next to content (API.md §0 <c>UserRef</c>).</summary>
public sealed record VariantUserRef(Guid Id, string Nickname);

/// <summary>A variant in lists (API.md §5 <c>VariantSummary</c>).</summary>
public sealed record VariantSummaryResponse(
    Guid Id,
    Guid RecipeId,
    string RecipeTitle,
    string Name,
    string? Description,
    VariantUserRef Author,
    VisibilityLevel Visibility,
    bool IsFavorite,
    bool BaseAvailable,
    int CookCount,
    int ChangeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static VariantSummaryResponse From(VariantSummaryModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        return new(
            m.Id, m.RecipeId, m.RecipeTitle, m.Name, m.Description, new VariantUserRef(m.AuthorId, m.AuthorNickname), m.Visibility,
            m.IsFavorite, m.BaseAvailable, m.CookCount, m.ChangeCount, m.CreatedAt, m.UpdatedAt);
    }
}

/// <summary>A page of variant summaries (API.md §0 <c>Paged</c>).</summary>
public sealed record VariantSummaryPage(IReadOnlyList<VariantSummaryResponse> Items, int Page, int PageSize, int TotalCount);

/// <summary>An ingredient operation in the API shape (API.md §5 <c>IngredientOp</c>; no server-side base amount).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ModifyIngredientOpDto), "modify")]
[JsonDerivedType(typeof(RemoveIngredientOpDto), "remove")]
[JsonDerivedType(typeof(AddIngredientOpDto), "add")]
public abstract record IngredientOpDto
{
    /// <summary>Maps an op; custom units of its quantity are looked up in <paramref name="units"/>.</summary>
    public static IngredientOpDto From(IngredientOp op, IReadOnlyDictionary<Guid, CustomUnit> units) => op switch
    {
        ModifyIngredientOp m => new ModifyIngredientOpDto(m.TargetId, m.ProductId, QuantityDto.From(m.Quantity, units), m.Note, m.Optional),
        RemoveIngredientOp r => new RemoveIngredientOpDto(r.TargetId),
        AddIngredientOp a => new AddIngredientOpDto(a.Id, a.AfterId, a.ProductId, QuantityDto.From(a.Quantity, units), a.Note, a.Optional),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown ingredient op."),
    };
}

/// <summary>Changes a base ingredient.</summary>
public sealed record ModifyIngredientOpDto(Guid TargetId, Guid ProductId, QuantityDto? Quantity, string? Note, bool Optional) : IngredientOpDto;

/// <summary>Removes a base ingredient.</summary>
public sealed record RemoveIngredientOpDto(Guid TargetId) : IngredientOpDto;

/// <summary>Adds an ingredient after <c>afterId</c> (null = at the start).</summary>
public sealed record AddIngredientOpDto(Guid Id, Guid? AfterId, Guid ProductId, QuantityDto? Quantity, string? Note, bool Optional) : IngredientOpDto;

/// <summary>A step operation (API.md §5 <c>StepOp</c>).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ReplaceStepOpDto), "replace")]
[JsonDerivedType(typeof(RemoveStepOpDto), "remove")]
[JsonDerivedType(typeof(AddStepOpDto), "add")]
public abstract record StepOpDto
{
    public static StepOpDto From(StepOp op) => op switch
    {
        ReplaceStepOp r => new ReplaceStepOpDto(r.Section, r.TargetId, r.Blocks),
        RemoveStepOp r => new RemoveStepOpDto(r.Section, r.TargetId),
        AddStepOp a => new AddStepOpDto(a.Section, a.Id, a.AfterId, a.Blocks),
        _ => throw new ArgumentOutOfRangeException(nameof(op), op, "Unknown step op."),
    };
}

/// <summary>Replaces the content of a base step.</summary>
public sealed record ReplaceStepOpDto(StepSection Section, Guid TargetId, IReadOnlyList<ContentBlock> Blocks) : StepOpDto;

/// <summary>Removes a base step.</summary>
public sealed record RemoveStepOpDto(StepSection Section, Guid TargetId) : StepOpDto;

/// <summary>Adds a step after <c>afterId</c> (null = at the start).</summary>
public sealed record AddStepOpDto(StepSection Section, Guid Id, Guid? AfterId, IReadOnlyList<ContentBlock> Blocks) : StepOpDto;

/// <summary>A variant with its operations (API.md §5 <c>VariantDetail</c>).</summary>
public sealed record VariantDetailResponse(
    Guid Id,
    Guid RecipeId,
    string RecipeTitle,
    string Name,
    string? Description,
    VariantUserRef Author,
    VisibilityLevel Visibility,
    bool IsFavorite,
    bool BaseAvailable,
    int CookCount,
    int ChangeCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    uint Version,
    IReadOnlyList<IngredientOpDto> IngredientOps,
    IReadOnlyList<StepOpDto> StepOps,
    IReadOnlyList<Guid> UtensilIdsAdded,
    IReadOnlyList<Guid> UtensilIdsRemoved,
    int? PrepMinutes,
    int? CookMinutes,
    int? ServingsOverride)
{
    public static VariantDetailResponse From(VariantDetailModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        var (m, v) = (model.Summary, model.Variant);
        return new(
            m.Id, m.RecipeId, m.RecipeTitle, m.Name, m.Description, new VariantUserRef(m.AuthorId, m.AuthorNickname), m.Visibility,
            m.IsFavorite, m.BaseAvailable, m.CookCount, m.ChangeCount, m.CreatedAt, m.UpdatedAt,
            v.Version,
            [.. v.IngredientOps.Select(op => IngredientOpDto.From(op, model.Units))],
            [.. v.StepOps.Select(StepOpDto.From)],
            v.UtensilIdsAdded,
            v.UtensilIdsRemoved,
            v.PrepMinutes,
            v.CookMinutes,
            v.ServingsOverride);
    }
}

/// <summary>A catalog product embedded in an ingredient (API.md §0 <c>ProductRef</c>).</summary>
public sealed record VariantProductRef(
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
    public static VariantProductRef From(Product p)
    {
        ArgumentNullException.ThrowIfNull(p);
        return new(p.Id, p.Name, p.Category, p.MeasureKind, p.DisplayClass, p.HasSizes, p.AllowHalf, p.GramsPerMl, p.GramsPerPiece, p.DefaultPackageAmount, p.PieceForms, p.IsVerified);
    }
}

/// <summary>An ingredient of a resolved recipe or a matrix option (API.md §4 <c>Ingredient</c>).</summary>
public sealed record VariantIngredientResponse(Guid Id, VariantProductRef Product, QuantityDto? Quantity, decimal? BaseAmount, string? Note, bool Optional)
{
    public static VariantIngredientResponse From(ResolvedIngredient i, IReadOnlyDictionary<Guid, Product> products, IReadOnlyDictionary<Guid, CustomUnit> units)
    {
        ArgumentNullException.ThrowIfNull(i);
        ArgumentNullException.ThrowIfNull(products);
        return new(i.Id, VariantProductRef.From(products[i.ProductId]), QuantityDto.From(i.Quantity, units), i.BaseAmount, i.Note, i.Optional);
    }
}

/// <summary>A step (API.md §4 <c>Step</c>).</summary>
public sealed record VariantStepResponse(Guid Id, IReadOnlyList<ContentBlock> Blocks);

/// <summary>A tag (API.md §0 <c>TagRef</c>).</summary>
public sealed record VariantTagRef(Guid Id, string Name, bool IsSystem);

/// <summary>A utensil (API.md §0 <c>UtensilRef</c>).</summary>
public sealed record VariantUtensilRef(Guid Id, string Name);

/// <summary>An image with its URLs (API.md §3 <c>ImageInfo</c>).</summary>
public sealed record VariantImageInfo(Guid Id, string Url, string ThumbnailUrl, int Width, int Height)
{
    public static VariantImageInfo? From(Image? image) => image is null
        ? null
        : new(image.Id, $"/api/v1/images/{image.Id}", $"/api/v1/images/{image.Id}?size=thumb", image.Width, image.Height);
}

/// <summary>A variant applied to its base (API.md §5 <c>ResolvedRecipe</c>); recipe fields describe the base recipe.</summary>
public sealed record ResolvedRecipeResponse(
    Guid Id,
    bool IsArchived,
    VisibilityLevel Visibility,
    bool IsFavorite,
    string Title,
    string? Description,
    VariantImageInfo? CoverImage,
    VariantUserRef Author,
    int? PrepMinutes,
    int? CookMinutes,
    int? Servings,
    IReadOnlyList<VariantTagRef> Tags,
    IReadOnlyList<VariantUtensilRef> Utensils,
    IReadOnlyList<VariantIngredientResponse> Ingredients,
    IReadOnlyList<VariantStepResponse> PrepSteps,
    IReadOnlyList<VariantStepResponse> InstructionSteps,
    int CookCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? VariantId,
    string? VariantName)
{
    public static ResolvedRecipeResponse From(ResolvedVariantModel m)
    {
        ArgumentNullException.ThrowIfNull(m);
        var (recipe, resolved) = (m.Recipe, m.Resolved);
        return new(
            recipe.Id,
            recipe.IsArchived,
            recipe.Visibility,
            m.RecipeIsFavorite,
            recipe.Title,
            recipe.Description,
            VariantImageInfo.From(recipe.CoverImage),
            new VariantUserRef(recipe.AuthorId, m.AuthorNickname),
            resolved.PrepMinutes,
            resolved.CookMinutes,
            resolved.Servings,
            [.. m.Tags.Select(t => new VariantTagRef(t.Id, t.Name, t.IsSystem))],
            [.. m.Utensils.Select(u => new VariantUtensilRef(u.Id, u.Name))],
            [.. resolved.Ingredients.Select(i => VariantIngredientResponse.From(i, m.Products, m.Units))],
            [.. resolved.PrepSteps.Select(s => new VariantStepResponse(s.Id, s.Blocks))],
            [.. resolved.InstructionSteps.Select(s => new VariantStepResponse(s.Id, s.Blocks))],
            m.RecipeCookCount,
            recipe.CreatedAt,
            recipe.UpdatedAt,
            m.Variant.Id,
            m.Variant.Name);
    }
}

/// <summary>A product swap of a variant (API.md §5.1).</summary>
public sealed record ProductSwapDto(Guid FromProductId, Guid ToProductId);

/// <summary>A column of the matrix; <c>id</c> is a variant id or <c>base</c> (API.md §5.1 <c>MatrixVariant</c>).</summary>
public sealed record MatrixVariantResponse(
    string Id,
    string Name,
    VariantUserRef Author,
    int CookCount,
    DateTimeOffset CreatedAt,
    bool IsFavorite,
    IReadOnlyList<ProductSwapDto> ProductSwaps);

/// <summary>One option of a slot (API.md §5.1 <c>SlotOption</c>).</summary>
public sealed record SlotOptionResponse(string Key, VariantIngredientResponse? Ingredient, IReadOnlyList<string> VariantIds);

/// <summary>A slot (API.md §5.1 <c>Slot</c>); <c>afterSlotKey</c> positions added slots among the base slots.</summary>
public sealed record SlotResponse(string Key, string Label, string? AfterSlotKey, IReadOnlyList<SlotOptionResponse> Options);

/// <summary>The input of client-side auto-match (API.md §5.1 <c>VariantMatrix</c>).</summary>
public sealed record VariantMatrixResponse(Guid RecipeId, IReadOnlyList<MatrixVariantResponse> Variants, IReadOnlyList<SlotResponse> Slots)
{
    public static VariantMatrixResponse From(VariantMatrixResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var products = result.Products;
        return new(
            result.Matrix.RecipeId,
            [.. result.Matrix.Variants.Zip(result.Columns, (v, c) => new MatrixVariantResponse(
                Id(v.VariantId), c.Name, new VariantUserRef(c.AuthorId, c.AuthorNickname), c.CookCount, c.CreatedAt, c.IsFavorite,
                [.. v.ProductSwaps.Select(s => new ProductSwapDto(s.FromProductId, s.ToProductId))]))],
            [.. result.Matrix.Slots.Select(s => new SlotResponse(
                s.Key,
                products[s.ProductId].Name,
                s.AfterSlotKey,
                [.. s.Options.Select(o => new SlotOptionResponse(
                    o.Key,
                    o.Ingredient is null ? null : VariantIngredientResponse.From(o.Ingredient, products, result.Units),
                    [.. o.VariantIds.Select(Id)]))]))]);
    }

    private static string Id(Guid? variantId) => variantId?.ToString() ?? MatrixKeys.BaseVariantId;
}

/// <summary>A remembered pick (API.md §5.2 <c>Pick</c>).</summary>
public sealed record PickDto([property: Required] string OptionKey, PickSource Source);

/// <summary>
/// The scaling choice (API.md §5.2 <c>ScalingState</c>): <c>"ingredient"</c> (the default when <c>kind</c> is absent) with
/// the user's "Mam: …" in base units, or <c>"servings"</c> with a number of servings. Responses always carry <c>kind</c>
/// and leave out the other form's fields. The shape is checked by <see cref="SelectionMemory"/>.
/// </summary>
public sealed record ScalingStateDto
{
    public ScalingKind? Kind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? AnchorSlotKey { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public decimal? AnchorBaseAmount { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Servings { get; init; }

    public ScalingState ToState() => new(AnchorSlotKey, AnchorBaseAmount, Kind ?? ScalingKind.Ingredient, Servings);

    public static ScalingStateDto From(ScalingState s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return s.Kind == ScalingKind.Servings
            ? new ScalingStateDto { Kind = ScalingKind.Servings, Servings = s.Servings }
            : new ScalingStateDto { Kind = ScalingKind.Ingredient, AnchorSlotKey = s.AnchorSlotKey, AnchorBaseAmount = s.AnchorBaseAmount };
    }
}

/// <summary>Picks and scaling to remember (API.md §5.2 <c>SelectionWrite</c>).</summary>
public sealed record SelectionWriteRequest
{
    /// <summary>Slot key → pick.</summary>
    [Required]
    public required IReadOnlyDictionary<string, PickDto> Picks { get; init; }

    public ScalingStateDto? Scaling { get; init; }

    public SelectionInput ToInput() => new(
        Picks.ToDictionary(p => p.Key, p => new SelectionPick(p.Value.OptionKey, p.Value.Source), StringComparer.Ordinal),
        Scaling?.ToState());
}

/// <summary>The remembered selection (API.md §5.2 <c>Selection</c>).</summary>
public sealed record SelectionResponse(IReadOnlyDictionary<string, PickDto> Picks, ScalingStateDto? Scaling, Guid RecipeId, DateTimeOffset UpdatedAt)
{
    public static SelectionResponse From(RecipeSelection s)
    {
        ArgumentNullException.ThrowIfNull(s);
        return new(
            s.Picks.ToDictionary(p => p.Key, p => new PickDto(p.Value.OptionKey, p.Value.Source), StringComparer.Ordinal),
            s.Scaling is null ? null : ScalingStateDto.From(s.Scaling),
            s.RecipeId,
            s.UpdatedAt);
    }
}
