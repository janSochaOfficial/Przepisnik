using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Catalog;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Units;
using RecipesList.Infrastructure.Persistence;

namespace RecipesList.Api.Features.Units;

/// <summary>
/// Admin edits of custom units (API.md §2.1, D-30): names and forms any time, the amount only while nothing uses the
/// unit, so an edit never silently changes other people's recipes. Also moves units when products are merged.
/// </summary>
internal sealed class UnitAdmin(AppDbContext db, ICurrentUser currentUser, IQuantityConverter converter)
{
    public async Task<CustomUnit> UpdateAsync(Guid id, CustomUnitInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!currentUser.IsAdmin)
        {
            throw new ForbiddenException(ErrorCodes.AdminRequired);
        }

        var unit = await db.CustomUnits.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new NotFoundException(ErrorCodes.UnitNotFound);

        if (input.ProductId != unit.ProductId)
        {
            throw new DomainValidationException(
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]> { ["productId"] = ["immutable"] });
        }

        Product? product = null;
        if (unit.ProductId is { } productId)
        {
            product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId, ct)
                ?? throw new NotFoundException(ErrorCodes.ProductNotFound);
        }

        var definition = CustomUnitRules.Define(input, product, converter);
        var amountChanges = definition.Amount != unit.Amount || definition.MeasureKind != unit.MeasureKind;
        if (amountChanges && await IsInUseAsync(id, ct))
        {
            throw new DomainValidationException(
                ErrorCodes.UnitInUse,
                new Dictionary<string, string[]> { ["equals"] = [ErrorCodes.UnitInUse] });
        }

        var scope = unit.ProductId;
        var clash = scope is { } p
            ? await db.CustomUnits.AnyAsync(u => u.Id != id && u.NormalizedName == definition.NormalizedName && u.ProductId == p, ct)
            : await db.CustomUnits.AnyAsync(u => u.Id != id && u.NormalizedName == definition.NormalizedName && u.ProductId == null, ct);
        if (clash)
        {
            throw new DomainValidationException(
                ErrorCodes.ValidationFailed,
                new Dictionary<string, string[]> { ["name"] = ["duplicate"] });
        }

        unit.Name = definition.Name;
        unit.NormalizedName = definition.NormalizedName;
        unit.Forms = definition.Forms;
        unit.MeasureKind = definition.MeasureKind;
        unit.Amount = definition.Amount;
        await db.SaveChangesAsync(ct);
        return unit;
    }

    /// <summary>
    /// Whether a recipe ingredient, a shopping-list item or a variant operation uses the unit. Variant ops are jsonb; ids are v7 GUIDs that are
    /// unique across entities, so a text search for the quoted id finds exactly the <c>customUnitId</c> fields.
    /// </summary>
    public async Task<bool> IsInUseAsync(Guid id, CancellationToken ct)
    {
        if (await db.RecipeIngredients.AnyAsync(i => i.QuantityCustomUnitId == id, ct))
        {
            return true;
        }

        if (await db.ShoppingListItems.AnyAsync(i => i.QuantityCustomUnitId == id, ct))
        {
            return true;
        }

        var text = id.ToString("D");
        return await db.Database
            .SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM variants WHERE strpos(ingredient_ops::text, {text}) > 0) AS \"Value\"")
            .SingleAsync(ct);
    }

    /// <summary>
    /// Product merge (PROJECT §7.3): the units bound to <paramref name="sourceId"/> move to <paramref name="targetId"/>.
    /// A name that clashes in the target's scope gets " (2)", " (3)" …. Joins the caller's transaction.
    /// </summary>
    public static async Task MoveProductUnitsAsync(AppDbContext db, Guid sourceId, Guid targetId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var moving = await db.CustomUnits.Where(u => u.ProductId == sourceId).OrderBy(u => u.CreatedAt).ThenBy(u => u.Id).ToListAsync(ct);
        if (moving.Count == 0)
        {
            return;
        }

        var taken = (await db.CustomUnits.Where(u => u.ProductId == targetId).Select(u => u.NormalizedName).ToListAsync(ct))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var unit in moving)
        {
            var name = CustomUnitRules.UniqueName(unit.Name, taken);
            unit.Name = name;
            unit.NormalizedName = NameNormalizer.Normalize(name);
            unit.ProductId = targetId;
            taken.Add(unit.NormalizedName);
        }

        await db.SaveChangesAsync(ct);
    }
}
