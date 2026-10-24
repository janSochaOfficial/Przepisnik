using Microsoft.EntityFrameworkCore;
using RecipesList.Core.Common;
using RecipesList.Core.Quantities;
using RecipesList.Core.Variants;

namespace RecipesList.Core.Units;

/// <summary>Loads the custom units that a set of quantities uses, in one query (no N+1, PROJECT §5.6).</summary>
public static class CustomUnitLookup
{
    /// <summary>The ids of the custom units of <paramref name="quantities"/>, without duplicates.</summary>
    public static List<Guid> CustomUnitIds(IEnumerable<Quantity?> quantities) =>
        [.. quantities.Select(q => q?.CustomUnitId).OfType<Guid>().Distinct()];

    /// <summary>The quantities inside ingredient operations (modify and add).</summary>
    public static IEnumerable<Quantity?> QuantitiesOf(IEnumerable<IngredientOp> ops) =>
        ops.Select(op => op switch
        {
            ModifyIngredientOp m => m.Quantity,
            AddIngredientOp a => a.Quantity,
            _ => null,
        });

    /// <summary>
    /// The custom units used by <paramref name="quantities"/>, by id (untracked). Units that no longer exist are absent.
    /// Runs no query when no quantity uses a custom unit.
    /// </summary>
    public static async Task<Dictionary<Guid, CustomUnit>> LoadCustomUnitsAsync(
        this IAppDbContext db,
        IEnumerable<Quantity?> quantities,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var ids = CustomUnitIds(quantities);
        return ids.Count == 0
            ? []
            : await db.CustomUnits.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, ct);
    }

    /// <summary>The unit of <paramref name="q"/> from <paramref name="units"/>, or null.</summary>
    public static CustomUnit? UnitOf(this IReadOnlyDictionary<Guid, CustomUnit> units, Quantity? q)
    {
        ArgumentNullException.ThrowIfNull(units);
        return q?.CustomUnitId is { } id && units.TryGetValue(id, out var unit) ? unit : null;
    }
}
