namespace RecipesList.Core.Quantities;

/// <summary>A unit an author can enter (PROJECT §4.2). JSON names: g, kg, ml, l, glass, tbsp, tsp, drop, pinch, piece.</summary>
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
}

/// <summary>Piece size for products with sizes (PROJECT §5.2).</summary>
public enum PieceSize
{
    Small,
    Medium,
    Large,
}

/// <summary>A quantity exactly as the author entered it. <see cref="Size"/> only matters for <see cref="Unit.Piece"/>.</summary>
public sealed record Quantity(decimal Value, Unit Unit, PieceSize? Size = null);
