using RecipesList.Core.Catalog;
using RecipesList.Core.Units;

namespace RecipesList.Core.Quantities;

/// <summary>Converts an entered quantity to the product's canonical base amount (PROJECT §5.1–5.2, §5.6).</summary>
public interface IQuantityConverter
{
    /// <summary>
    /// Returns the base amount (g, ml or medium-equivalent pieces, depending on <see cref="Product.MeasureKind"/>),
    /// <c>null</c> for "do smaku" (<paramref name="q"/> is null) and <c>null</c> when the unit cannot be converted
    /// for this product (missing density or piece weight). Validation of the value itself (&gt; 0) is the caller's job.
    /// A <see cref="Unit.Custom"/> quantity needs its unit: use the overload that takes it (here it gives <c>null</c>).
    /// </summary>
    decimal? ToBaseAmount(Quantity? q, Product product);

    /// <summary>
    /// As <see cref="ToBaseAmount(Quantity?, Product)"/>, with the definition of the quantity's custom unit (PROJECT §5.6):
    /// <c>value × amount</c> in the unit's measure kind, then the normal conversion to the product's kind. <c>null</c>
    /// when the unit is missing, is not the quantity's unit, or belongs to another product.
    /// </summary>
    decimal? ToBaseAmount(Quantity? q, Product product, CustomUnit? customUnit);
}

/// <summary>The fixed conversion constants (D-07). The frontend mirrors them; shared vectors keep both in sync.</summary>
public static class QuantityConstants
{
    public const decimal MlPerGlass = 250m;
    public const decimal MlPerTablespoon = 15m;
    public const decimal MlPerTeaspoon = 5m;
    public const decimal MlPerDrop = 0.05m;
    public const decimal MlPerPinch = 0.3m;
    public const decimal MlPerLitre = 1000m;
    public const decimal GramsPerKilogram = 1000m;

    public const decimal SmallPieceFactor = 0.75m;
    public const decimal MediumPieceFactor = 1m;
    public const decimal LargePieceFactor = 1.3m;

    /// <summary>Base amounts are rounded to this many decimals (the frontend does the same).</summary>
    public const int BaseAmountDecimals = 4;

    public static decimal SizeFactor(PieceSize size) => size switch
    {
        PieceSize.Small => SmallPieceFactor,
        PieceSize.Large => LargePieceFactor,
        _ => MediumPieceFactor,
    };
}

/// <inheritdoc />
public sealed class QuantityConverter : IQuantityConverter
{
    /// <inheritdoc />
    public decimal? ToBaseAmount(Quantity? q, Product product) => ToBaseAmount(q, product, null);

    /// <inheritdoc />
    public decimal? ToBaseAmount(Quantity? q, Product product, CustomUnit? customUnit)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (q is null)
        {
            return null;
        }

        (MeasureKind Kind, decimal Amount)? native;
        if (q.Unit == Unit.Custom)
        {
            // A product unit is offered (and valid) for its own product only.
            native = customUnit is not null && customUnit.Id == q.CustomUnitId
                && (customUnit.ProductId is null || customUnit.ProductId == product.Id)
                ? (customUnit.MeasureKind, q.Value * customUnit.Amount)
                : null;
        }
        else
        {
            native = NativeAmount(q, product.HasSizes);
        }

        if (native is not { } n)
        {
            return null;
        }

        var converted = ConvertKind(n.Amount, n.Kind, product.MeasureKind, product);
        return converted is null ? null : RoundBase(converted.Value);
    }

    /// <summary>Rounds a base amount to <see cref="QuantityConstants.BaseAmountDecimals"/>, half away from zero.</summary>
    public static decimal RoundBase(decimal amount) =>
        Math.Round(amount, QuantityConstants.BaseAmountDecimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The amount of a built-in quantity in its unit's own base unit: grams, millilitres or medium pieces. A size counts
    /// only when <paramref name="useSizes"/> (the product has sizes); otherwise every piece is a medium piece.
    /// <c>null</c> for <see cref="Unit.Custom"/>, which has no native kind of its own.
    /// </summary>
    public static (MeasureKind Kind, decimal Amount)? NativeAmount(Quantity q, bool useSizes)
    {
        ArgumentNullException.ThrowIfNull(q);
        return q.Unit switch
        {
            Unit.G => (MeasureKind.Mass, q.Value),
            Unit.Kg => (MeasureKind.Mass, q.Value * QuantityConstants.GramsPerKilogram),
            Unit.Ml => (MeasureKind.Volume, q.Value),
            Unit.L => (MeasureKind.Volume, q.Value * QuantityConstants.MlPerLitre),
            Unit.Glass => (MeasureKind.Volume, q.Value * QuantityConstants.MlPerGlass),
            Unit.Tbsp => (MeasureKind.Volume, q.Value * QuantityConstants.MlPerTablespoon),
            Unit.Tsp => (MeasureKind.Volume, q.Value * QuantityConstants.MlPerTeaspoon),
            Unit.Drop => (MeasureKind.Volume, q.Value * QuantityConstants.MlPerDrop),
            Unit.Pinch => (MeasureKind.Volume, q.Value * QuantityConstants.MlPerPinch),
            Unit.Piece => (MeasureKind.Count, q.Value * (useSizes && q.Size is { } size ? QuantityConstants.SizeFactor(size) : 1m)),
            Unit.Custom => null,
            _ => throw new ArgumentOutOfRangeException(nameof(q), q.Unit, "Unknown unit."),
        };
    }

    /// <summary>
    /// Converts an amount between measure kinds with the product's density (mass ↔ volume) and piece weight
    /// (count ↔ mass); <c>null</c> when the product lacks the needed data.
    /// </summary>
    public static decimal? ConvertKind(decimal amount, MeasureKind from, MeasureKind to, Product p)
    {
        ArgumentNullException.ThrowIfNull(p);
        if (from == to)
        {
            return amount;
        }

        var grams = from switch
        {
            MeasureKind.Mass => amount,
            MeasureKind.Volume => p.GramsPerMl is { } density and > 0 ? amount * density : (decimal?)null,
            MeasureKind.Count => p.GramsPerPiece is { } pieceWeight and > 0 ? amount * pieceWeight : (decimal?)null,
            _ => null,
        };
        if (grams is null)
        {
            return null;
        }

        return to switch
        {
            MeasureKind.Mass => grams,
            MeasureKind.Volume => p.GramsPerMl is { } density and > 0 ? grams / density : null,
            MeasureKind.Count => p.GramsPerPiece is { } pieceWeight and > 0 ? grams / pieceWeight : null,
            _ => null,
        };
    }
}
