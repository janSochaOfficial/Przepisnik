using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;

namespace RecipesList.Api.Features.Catalog;

/// <summary>
/// Read and create side of the product catalog (API.md §2, PROJECT §7). Search ranks an exact match above prefix matches above
/// word-prefix matches above trigram similarity, all on the normalized name; within each tier verified products come first (D-32).
/// </summary>
internal sealed class ProductCatalog(IAppDbContext db, ICurrentUser currentUser, TimeProvider time)
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 50;

    /// <summary>The minimum <c>similarity()</c> for a fuzzy match (PostgreSQL's own default threshold).</summary>
    public const float SimilarityThreshold = 0.3f;

    public async Task<List<Product>> SearchAsync(string? q, ProductCategory? category, int? limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var products = db.Products.AsNoTracking();
        if (category is { } c)
        {
            products = products.Where(p => p.Category == c);
        }

        var term = NameNormalizer.Normalize(q ?? "");
        if (term.Length == 0)
        {
            return await products.OrderByDescending(p => p.IsVerified).ThenBy(p => p.NormalizedName).Take(take).ToListAsync(ct);
        }

        var escaped = NameSearch.EscapeLike(term);
        var prefix = escaped + "%";
        var wordPrefix = "% " + escaped + "%";

        return await products
            .Where(p => EF.Functions.Like(p.NormalizedName, prefix, NameSearch.LikeEscape)
                || EF.Functions.Like(p.NormalizedName, wordPrefix, NameSearch.LikeEscape)
                || EF.Functions.TrigramsSimilarity(p.NormalizedName, term) > SimilarityThreshold)
            .OrderBy(p => p.NormalizedName == term ? 0
                : EF.Functions.Like(p.NormalizedName, prefix, NameSearch.LikeEscape) ? 1
                : EF.Functions.Like(p.NormalizedName, wordPrefix, NameSearch.LikeEscape) ? 2 : 3)
            .ThenByDescending(p => p.IsVerified)
            .ThenByDescending(p => EF.Functions.TrigramsSimilarity(p.NormalizedName, term))
            .ThenBy(p => p.NormalizedName)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<Product> GetAsync(Guid id, CancellationToken ct) =>
        await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.ProductNotFound);

    public async Task<Product> CreateAsync(ProductInput input, CancellationToken ct)
    {
        ProductRules.Validate(input);
        var name = ProductRules.CleanName(input.Name);
        var normalized = NameNormalizer.Normalize(name);

        await ThrowIfDuplicateAsync(normalized, exceptId: null, ct);

        var product = new Product
        {
            Name = name,
            NormalizedName = normalized,
            CreatedById = currentUser.Id,
            IsVerified = currentUser.IsAdmin,
            CreatedAt = time.GetUtcNow(),
        };
        ProductMapping.Apply(product, input);
        db.Products.Add(product);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request created the same name between the check and the insert.
            await ThrowIfDuplicateAsync(normalized, exceptId: null, ct);
            throw;
        }

        return product;
    }

    /// <summary>409 <c>product.duplicate</c> with <c>existingId</c> when another product has this normalized name.</summary>
    internal async Task ThrowIfDuplicateAsync(string normalized, Guid? exceptId, CancellationToken ct)
    {
        var except = exceptId ?? Guid.Empty;
        var existing = await db.Products
            .AsNoTracking()
            .Where(p => p.NormalizedName == normalized && p.Id != except)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is { } id)
        {
            throw new ConflictException(ErrorCodes.ProductDuplicate).With("existingId", id);
        }
    }
}

/// <summary>Copies the writable fields of a <see cref="ProductInput"/> onto a product.</summary>
internal static class ProductMapping
{
    public static void Apply(Product product, ProductInput input)
    {
        product.Category = input.Category;
        product.MeasureKind = input.MeasureKind;
        product.DisplayClass = input.DisplayClass;
        product.HasSizes = input.HasSizes;
        product.AllowHalf = input.AllowHalf;
        product.GramsPerMl = input.GramsPerMl;
        product.GramsPerPiece = input.GramsPerPiece;
        product.DefaultPackageAmount = input.DefaultPackageAmount;
        product.PieceForms = input.PieceForms;
    }
}

/// <summary>Shared pieces of the name searches.</summary>
internal static class NameSearch
{
    public const string LikeEscape = "\\";

    /// <summary>Escapes <c>%</c>, <c>_</c> and the escape character itself for a LIKE pattern.</summary>
    public static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
}
