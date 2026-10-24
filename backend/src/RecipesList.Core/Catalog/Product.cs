namespace RecipesList.Core.Catalog;

/// <summary>Canonical measure of a product; decides the base unit (PROJECT §5.1).</summary>
public enum MeasureKind
{
    /// <summary>Grams.</summary>
    Mass,

    /// <summary>Millilitres.</summary>
    Volume,

    /// <summary>Medium-equivalent pieces.</summary>
    Count,
}

/// <summary>How scaled amounts are shown (PROJECT §5.1, §5.4).</summary>
public enum DisplayClass
{
    Weight,
    Liquid,
    Spice,
    Piece,
}

/// <summary>Product category (PROJECT §7.2).</summary>
public enum ProductCategory
{
    Vegetables,
    Fruits,
    Meat,
    Fish,
    Dairy,
    Bakery,
    DryGoods,
    Spices,
    OilsVinegars,
    Sauces,
    Canned,
    Frozen,
    Drinks,
    Sweets,
    Other,
}

/// <summary>Polish plural forms of a piece name (PROJECT §5.5), e.g. ząbek / ząbki / ząbków / ząbka.</summary>
public sealed record PluralForms(string One, string Few, string Many, string Fraction);

/// <summary>An entry of the shared, growing product catalog (PROJECT §7).</summary>
public sealed class Product
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    /// <summary><see cref="Common.NameNormalizer"/> form of <see cref="Name"/>; unique, trigram-indexed.</summary>
    public required string NormalizedName { get; set; }

    public ProductCategory Category { get; set; }

    public MeasureKind MeasureKind { get; set; }

    public DisplayClass DisplayClass { get; set; }

    public bool HasSizes { get; set; }

    public bool AllowHalf { get; set; }

    /// <summary>Density used for mass ↔ volume conversion.</summary>
    public decimal? GramsPerMl { get; set; }

    /// <summary>Weight of one medium piece, used for count ↔ mass conversion.</summary>
    public decimal? GramsPerPiece { get; set; }

    /// <summary>Typical package size in base units (e.g. 330 ml of cream).</summary>
    public decimal? DefaultPackageAmount { get; set; }

    /// <summary>Custom piece names (jsonb); null means "szt.".</summary>
    public PluralForms? PieceForms { get; set; }

    /// <summary>True for seeded and admin-curated products; user-added products rank below them in search (D-32).</summary>
    public bool IsVerified { get; set; }

    /// <summary>Null for seeded products.</summary>
    public Guid? CreatedById { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
