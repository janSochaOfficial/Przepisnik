using Microsoft.EntityFrameworkCore;
using RecipesList.Api.Features.Catalog;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Units;

namespace RecipesList.Api.Features.Units;

/// <summary>Listing and idempotent create of custom units (API.md §2.1, PROJECT §5.6).</summary>
internal sealed class UnitCatalog(IAppDbContext db, ICurrentUser currentUser, IQuantityConverter converter, TimeProvider time)
{
    public const int MaxLimit = 50;

    /// <summary>
    /// The units for every product, plus the units of <paramref name="productId"/> when given, optionally filtered by
    /// a normalized substring of the name. Product units come first, then by name.
    /// </summary>
    public async Task<List<CustomUnit>> SearchAsync(Guid? productId, string? q, int? limit, CancellationToken ct)
    {
        var take = Math.Clamp(limit ?? MaxLimit, 1, MaxLimit);
        var units = productId is { } id
            ? db.CustomUnits.AsNoTracking().Where(u => u.ProductId == null || u.ProductId == id)
            : db.CustomUnits.AsNoTracking().Where(u => u.ProductId == null);

        var term = NameNormalizer.Normalize(q ?? "");
        if (term.Length > 0)
        {
            var pattern = "%" + NameSearch.EscapeLike(term) + "%";
            units = units.Where(u => EF.Functions.Like(u.NormalizedName, pattern, NameSearch.LikeEscape));
        }

        return await units
            .OrderBy(u => u.ProductId == null ? 1 : 0)
            .ThenBy(u => u.NormalizedName)
            .ThenBy(u => u.Id)
            .Take(take)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Creates a unit, or returns the one with the same normalized name in the same scope (the same product, or every
    /// product). 404 <c>product.not_found</c>, 400 <c>unit.invalid</c> or <c>validation.failed</c> otherwise.
    /// </summary>
    public async Task<CreateResult<CustomUnit>> CreateAsync(CustomUnitInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        Product? product = null;
        if (input.ProductId is { } productId)
        {
            product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new NotFoundException(ErrorCodes.ProductNotFound);
        }

        var definition = CustomUnitRules.Define(input, product, converter);
        if (await FindAsync(definition.NormalizedName, input.ProductId, ct) is { } existing)
        {
            return new(existing, false);
        }

        var unit = new CustomUnit
        {
            Name = definition.Name,
            NormalizedName = definition.NormalizedName,
            Forms = definition.Forms,
            ProductId = input.ProductId,
            MeasureKind = definition.MeasureKind,
            Amount = definition.Amount,
            CreatedById = currentUser.Id,
            CreatedAt = time.GetUtcNow(),
        };
        db.CustomUnits.Add(unit);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request created the same name in the same scope: return that one.
            return new(await FindAsync(definition.NormalizedName, input.ProductId, ct) ?? throw new InvalidOperationException("Unit insert failed."), false);
        }

        return new(unit, true);
    }

    private Task<CustomUnit?> FindAsync(string normalizedName, Guid? productId, CancellationToken ct) => productId is { } id
        ? db.CustomUnits.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedName == normalizedName && u.ProductId == id, ct)
        : db.CustomUnits.AsNoTracking().FirstOrDefaultAsync(u => u.NormalizedName == normalizedName && u.ProductId == null, ct);
}
