using RecipesList.Core.Catalog;

namespace RecipesList.Core.Units;

/// <summary>
/// An entry of the shared custom-unit catalog (PROJECT §5.6, D-30): "1 <see cref="Name"/> = <see cref="Amount"/> base
/// units of <see cref="MeasureKind"/>". A unit with a <see cref="ProductId"/> is offered for that product only; without
/// one it is offered for every product it converts to.
/// </summary>
public sealed class CustomUnit
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    /// <summary><see cref="Common.NameNormalizer"/> form of <see cref="Name"/>; unique per scope (product or none).</summary>
    public required string NormalizedName { get; set; }

    /// <summary>The four Polish plural forms (jsonb), e.g. puszka / puszki / puszek / puszki.</summary>
    public required PluralForms Forms { get; set; }

    /// <summary>The product the unit belongs to; null for a unit for every product.</summary>
    public Guid? ProductId { get; set; }

    /// <summary>The kind <see cref="Amount"/> is measured in; for a product unit it is the product's kind.</summary>
    public MeasureKind MeasureKind { get; set; }

    /// <summary>Base units (g, ml or medium pieces) in one unit, rounded to 4 decimals.</summary>
    public decimal Amount { get; set; }

    /// <summary>Null when the creator was deleted.</summary>
    public Guid? CreatedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
