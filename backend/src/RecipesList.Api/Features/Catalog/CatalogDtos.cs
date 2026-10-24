using RecipesList.Core.Catalog;

namespace RecipesList.Api.Features.Catalog;

// Request and response shapes of API.md §2.

/// <summary>A catalog product (API.md §0 <c>ProductRef</c>).</summary>
internal sealed record CatalogProductDto(
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
    PluralForms? PieceForms)
{
    public static CatalogProductDto From(Product p) => new(
        p.Id, p.Name, p.Category, p.MeasureKind, p.DisplayClass, p.HasSizes, p.AllowHalf,
        p.GramsPerMl, p.GramsPerPiece, p.DefaultPackageAmount, p.PieceForms);
}

/// <summary>A tag (API.md §0 <c>TagRef</c>).</summary>
internal sealed record CatalogTagDto(Guid Id, string Name, bool IsSystem)
{
    public static CatalogTagDto From(Tag t) => new(t.Id, t.Name, t.IsSystem);
}

/// <summary>A utensil (API.md §0 <c>UtensilRef</c>).</summary>
internal sealed record CatalogUtensilDto(Guid Id, string Name)
{
    public static CatalogUtensilDto From(Utensil u) => new(u.Id, u.Name);
}

/// <summary>Body of <c>POST /products</c> and <c>PUT /admin/products/{id}</c> (API.md §2 <c>ProductWrite</c>).</summary>
internal sealed record ProductWriteRequest
{
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

    public ProductInput ToInput() => new(
        Name, Category, MeasureKind, DisplayClass, HasSizes, AllowHalf,
        GramsPerMl, GramsPerPiece, DefaultPackageAmount, PieceForms);
}

/// <summary>Body of <c>POST /admin/products/{id}/merge</c>.</summary>
internal sealed record MergeProductRequest
{
    public required Guid TargetId { get; init; }
}

/// <summary>Body of <c>POST /tags</c> and <c>POST /utensils</c> (API.md §2 <c>NameWrite</c>).</summary>
internal sealed record NameWriteRequest
{
    public required string Name { get; init; }
}
