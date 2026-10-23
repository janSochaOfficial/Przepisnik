using RecipesList.Core.Catalog;

namespace RecipesList.Core.Quantities;

/// <summary>Converts an entered quantity to the product's canonical base amount (PROJECT §5.1–5.2).</summary>
public interface IQuantityConverter
{
    /// <summary>
    /// Returns the base amount (g, ml or medium-equivalent pieces, depending on <see cref="Product.MeasureKind"/>),
    /// <c>null</c> for "do smaku" (<paramref name="q"/> is null) and <c>null</c> when the unit cannot be converted
    /// for this product (missing density or piece weight). Validation of the value itself (&gt; 0) is the caller's job.
    /// </summary>
    decimal? ToBaseAmount(Quantity? q, Product product);
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
    public decimal? ToBaseAmount(Quantity? q, Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (q is null)
        {
            return null;
        }

        var (kind, amount) = ToNativeAmount(q, product);
        var converted = Convert(amount, kind, product.MeasureKind, product);
        return converted is null
            ? null
            : Math.Round(converted.Value, QuantityConstants.BaseAmountDecimals, MidpointRounding.AwayFromZero);
    }

    // The amount in the unit's own base unit: grams, millilitres or medium pieces.
    private static (MeasureKind Kind, decimal Amount) ToNativeAmount(Quantity q, Product product) => q.Unit switch
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
        // A size only counts for products that have sizes; otherwise every piece is a "medium" piece.
        Unit.Piece => (MeasureKind.Count, q.Value * (product.HasSizes && q.Size is { } size ? QuantityConstants.SizeFactor(size) : 1m)),
        _ => throw new ArgumentOutOfRangeException(nameof(q), q.Unit, "Unknown unit."),
    };

    private static decimal? Convert(decimal amount, MeasureKind from, MeasureKind to, Product p)
    {
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
