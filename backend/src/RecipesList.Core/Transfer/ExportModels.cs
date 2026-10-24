using System.Text.Json.Serialization;
using RecipesList.Core.Catalog;
using RecipesList.Core.Quantities;
using RecipesList.Core.Recipes;

namespace RecipesList.Core.Transfer;

/// <summary>Constants of the recipe export format (API.md §8, PROJECT §4.7).</summary>
public static class ExportFormat
{
    public const string Name = "recipes-list/recipe";

    public const int Version = 1;

    /// <summary>The only content type of an embedded image.</summary>
    public const string ImageContentType = "image/webp";

    /// <summary>The import limit for the whole file (PROJECT §4.7).</summary>
    public const long MaxFileBytes = 25L * 1024 * 1024;
}

/// <summary>A self-contained recipe file (API.md §8 <c>RecipeExport</c>). Keys are opaque strings, unique within the file.</summary>
public sealed record RecipeExportDocument
{
    public required string Format { get; init; }

    public required int FormatVersion { get; init; }

    public required DateTimeOffset ExportedAt { get; init; }

    public required IReadOnlyList<ExportProduct> Products { get; init; }

    /// <summary>Every custom unit referenced in the file; absent (null) in files without any (PROJECT §5.6).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<ExportCustomUnit>? CustomUnits { get; init; }

    /// <summary>Local image key → image.</summary>
    public required IReadOnlyDictionary<string, ExportImage> Images { get; init; }

    public required ExportRecipe Recipe { get; init; }

    public required IReadOnlyList<ExportVariant> Variants { get; init; }
}

/// <summary>An embedded photo.</summary>
public sealed record ExportImage
{
    public required string ContentType { get; init; }

    public required string DataBase64 { get; init; }
}

/// <summary>A product with the full <c>ProductWrite</c> properties, so import can create it when it is missing.</summary>
public sealed record ExportProduct
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required ProductCategory Category { get; init; }

    public required MeasureKind MeasureKind { get; init; }

    public required DisplayClass DisplayClass { get; init; }

    public required bool HasSizes { get; init; }

    public required bool AllowHalf { get; init; }

    public decimal? GramsPerMl { get; init; }

    public decimal? GramsPerPiece { get; init; }

    public decimal? DefaultPackageAmount { get; init; }

    public PluralForms? PieceForms { get; init; }
}

/// <summary>
/// A custom unit with its definition in base units, so import can match or create it: "1 <see cref="Name"/>" =
/// <see cref="Amount"/> base units of <see cref="MeasureKind"/>. <see cref="ProductKey"/> is null for a unit for every product.
/// </summary>
public sealed record ExportCustomUnit
{
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required PluralForms Forms { get; init; }

    public string? ProductKey { get; init; }

    public required MeasureKind MeasureKind { get; init; }

    public required decimal Amount { get; init; }
}

/// <summary>
/// A quantity of the file (API.md §8 <c>ExportQuantity</c>): <c>Quantity</c> with <see cref="CustomUnitKey"/> (a key of
/// <see cref="RecipeExportDocument.CustomUnits"/>, set iff the unit is <see cref="Unit.Custom"/>) instead of an id.
/// The key is left out when null, so files without custom units look exactly as before.
/// </summary>
public sealed record ExportQuantity(
    decimal Value,
    Unit Unit,
    PieceSize? Size = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CustomUnitKey = null);

/// <summary>The base recipe of the file.</summary>
public sealed record ExportRecipe
{
    public required string Key { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    public string? CoverImageKey { get; init; }

    public required string AuthorNickname { get; init; }

    public int? PrepMinutes { get; init; }

    public int? CookMinutes { get; init; }

    public required IReadOnlyList<string> Tags { get; init; }

    public required IReadOnlyList<string> Utensils { get; init; }

    public required IReadOnlyList<ExportIngredient> Ingredients { get; init; }

    public required IReadOnlyList<ExportStep> PrepSteps { get; init; }

    public required IReadOnlyList<ExportStep> InstructionSteps { get; init; }
}

/// <summary>An ingredient row.</summary>
public sealed record ExportIngredient
{
    public required string Key { get; init; }

    public required string ProductKey { get; init; }

    public ExportQuantity? Quantity { get; init; }

    public string? Note { get; init; }

    public required bool Optional { get; init; }
}

/// <summary>A step.</summary>
public sealed record ExportStep
{
    public required string Key { get; init; }

    public required IReadOnlyList<ExportBlock> Blocks { get; init; }
}

/// <summary>Step content: text or a photo (by image key).</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ExportTextBlock), "text")]
[JsonDerivedType(typeof(ExportImageBlock), "image")]
public abstract record ExportBlock;

/// <summary>Plain text.</summary>
public sealed record ExportTextBlock(string Text) : ExportBlock;

/// <summary>A photo with an optional caption.</summary>
public sealed record ExportImageBlock(string ImageKey, string? Caption = null) : ExportBlock;

/// <summary>A variant: operations against the recipe of the file, with keys instead of ids.</summary>
public sealed record ExportVariant
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string AuthorNickname { get; init; }

    public int? PrepMinutes { get; init; }

    public int? CookMinutes { get; init; }

    public required IReadOnlyList<string> UtensilsAdded { get; init; }

    public required IReadOnlyList<string> UtensilsRemoved { get; init; }

    public required IReadOnlyList<ExportIngredientOp> IngredientOps { get; init; }

    public required IReadOnlyList<ExportStepOp> StepOps { get; init; }
}

/// <summary>
/// An ingredient operation (API.md §5 <c>IngredientOp</c>) with <c>id</c> → <c>key</c>, <c>targetId</c> → <c>targetKey</c>,
/// <c>afterId</c> → <c>afterKey</c> and <c>productId</c> → <c>productKey</c>; quantities are <see cref="ExportQuantity"/>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ExportModifyIngredientOp), "modify")]
[JsonDerivedType(typeof(ExportRemoveIngredientOp), "remove")]
[JsonDerivedType(typeof(ExportAddIngredientOp), "add")]
public abstract record ExportIngredientOp;

public sealed record ExportModifyIngredientOp(string TargetKey, string ProductKey, ExportQuantity? Quantity, string? Note, bool Optional) : ExportIngredientOp;

public sealed record ExportRemoveIngredientOp(string TargetKey) : ExportIngredientOp;

public sealed record ExportAddIngredientOp(string Key, string? AfterKey, string ProductKey, ExportQuantity? Quantity, string? Note, bool Optional) : ExportIngredientOp;

/// <summary>A step operation (API.md §5 <c>StepOp</c>) with keys instead of ids and <c>imageKey</c> in its blocks.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "op")]
[JsonDerivedType(typeof(ExportReplaceStepOp), "replace")]
[JsonDerivedType(typeof(ExportRemoveStepOp), "remove")]
[JsonDerivedType(typeof(ExportAddStepOp), "add")]
public abstract record ExportStepOp(StepSection Section);

public sealed record ExportReplaceStepOp(StepSection Section, string TargetKey, IReadOnlyList<ExportBlock> Blocks) : ExportStepOp(Section);

public sealed record ExportRemoveStepOp(StepSection Section, string TargetKey) : ExportStepOp(Section);

public sealed record ExportAddStepOp(StepSection Section, string Key, string? AfterKey, IReadOnlyList<ExportBlock> Blocks) : ExportStepOp(Section);
