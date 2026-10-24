namespace RecipesList.Core.Quantities;

/// <summary>
/// A unit an author can enter (PROJECT §4.2). JSON names: g, kg, ml, l, glass, tbsp, tsp, drop, pinch, piece, custom.
/// <see cref="Custom"/> is a unit of the custom-unit catalog (PROJECT §5.6), named by <see cref="Quantity.CustomUnitId"/>.
/// </summary>
public enum Unit
{
    G,
    Kg,
    Ml,
    L,
    Glass,
    Tbsp,
    Tsp,
    Drop,
    Pinch,
    Piece,
    Custom,
}

/// <summary>Piece size for products with sizes (PROJECT §5.2).</summary>
public enum PieceSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// A quantity exactly as the author entered it. <see cref="Size"/> only matters for <see cref="Unit.Piece"/>;
/// <see cref="CustomUnitId"/> is set iff <see cref="Unit"/> is <see cref="Unit.Custom"/> (API.md §0).
/// </summary>
public sealed record Quantity(decimal Value, Unit Unit, PieceSize? Size = null, Guid? CustomUnitId = null)
{
    /// <summary>
    /// The shape rules that need no database: a positive value, a size only with pieces, and a custom unit id exactly
    /// when the unit is <see cref="Unit.Custom"/>. Whether the product has sizes is checked by the callers that know it.
    /// </summary>
    public bool IsWellFormed =>
        Value > 0
        && (Size is null || Unit == Unit.Piece)
        && (Unit == Unit.Custom) == (CustomUnitId is not null);
}
